using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncRoo.Interfaces;
using SyncRoo.Models;
using SyncRoo.Models.Dtos;
using SyncRoo.Utils;

namespace SyncRoo.Core
{
    public class Engine(CommandOptions commandOptions, IOptions<AppSyncSettings> syncSettings, IFileStorageProvider fileStorageProvider, ILogger logger)
    {
        private readonly AppSyncSettings syncSettings = syncSettings.Value;
        private readonly SyncReport syncReport = new();

        public async Task Sync()
        {
            syncReport.StartedTime = DateTime.Now;

            await fileStorageProvider.Initialize(commandOptions.DatabaseConnectionString, logger);

            if (!string.IsNullOrWhiteSpace(commandOptions.Operation))
            {
                switch (commandOptions.Operation)
                {
                    case Operations.Scan:
                        await ScanFiles(commandOptions.SourceFolder, SyncFileMode.Source);
                        await ScanFiles(commandOptions.TargetFolder, SyncFileMode.Target);
                        break;
                    case Operations.Process:
                        await ProcessPendingFiles();
                        break;
                    case Operations.Run:
                        await GenerateAndRunBatchFiles();
                        break;
                }
            }
            else
            {
                await ScanFiles(commandOptions.SourceFolder, SyncFileMode.Source);
                await ScanFiles(commandOptions.TargetFolder, SyncFileMode.Target);
                await ProcessPendingFiles();
                await GenerateAndRunBatchFiles();
            }

            if (commandOptions.AutoTeardown)
            {
                await fileStorageProvider.Teardown(commandOptions.DatabaseConnectionString, logger);
            }

            syncReport.FinishedTime = DateTime.Now;

            ProduceReport();
        }

        private void ProduceReport()
        {
            logger.LogInformation("Sync report");
            logger.LogInformation("\tStart time: {StartTime}", syncReport.StartedTime);
            logger.LogInformation("\tFinish time: {FinishTime}", syncReport.FinishedTime);
            logger.LogInformation("\tDuration: {Duration}", syncReport.Timer.Elapsed.ToReadableTimespan());
            logger.LogInformation("\tSource file count: {SourceFileCount}", syncReport.SourceFileCount);
            logger.LogInformation("\tTarget file count: {TargetFileCount}", syncReport.TargetFileCount);
            logger.LogInformation("\tProcessed file count: {ProcessedFileCount}", syncReport.ProcessedFileCount);
            logger.LogInformation("\tProcessed file size: {ProcessedFileSize}", FileUtils.FormatSize(syncReport.ProcessedFileBytes));
        }

        private async Task GenerateAndRunBatchFiles()
        {
            var batchFiles = await GenerateBatchFiles();

            if (batchFiles.Count == 0)
            {
                logger.LogInformation("No batch files generated.");
            }

            Parallel.ForEach(batchFiles, new ParallelOptions
            {
                MaxDegreeOfParallelism = commandOptions.MultiThreads,
                TaskScheduler = TaskScheduler.Default
            }, batchFile =>
            {
                try
                {
                    logger.LogInformation("Running batch file {BatchFile}...", batchFile);

                    var process = new Process();
                    process.StartInfo.FileName = batchFile;
                    process.StartInfo.ErrorDialog = true;
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                    process.WaitForExit(TimeSpan.FromSeconds(syncSettings.ProcessTimeoutInSeconds));

                    batchFile.SafeDelete();

                    logger.LogInformation("Ran batch file {BatchFile}.", batchFile);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to run batch file {BatchFile}", batchFile);
                }
            });
        }

        private async Task<List<string>> GenerateBatchFiles()
        {
            const string DefaultBatchFolder = "Batch";
            using var connection = new SqlConnection(commandOptions.DatabaseConnectionString);
            var batchFolder = commandOptions.BatchFolder;

            if (string.IsNullOrWhiteSpace(batchFolder))
            {
                batchFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultBatchFolder);
            }

            if (!Directory.Exists(batchFolder))
            {
                Directory.CreateDirectory(batchFolder);
            }

            logger.LogInformation("Generating batch files in {BatchFolder}...", batchFolder);

            var runId = DateTime.Now.ToString("yyyMMdd-HHmmss");
            var batchRunFolder = Path.Combine(batchFolder, runId);

            if (!Directory.Exists(batchRunFolder))
            {
                Directory.CreateDirectory(batchRunFolder);
            }

            logger.LogInformation("Batch files will be generated in {BatchFolder} for this run...", batchRunFolder);

            var batchFiles = new List<string>();
            var lastId = 0L;
            var batchFileCount = 0;
            var pendingFileCount = await fileStorageProvider.GetPendingFileCount(commandOptions.DatabaseConnectionString);

            if (pendingFileCount == 0)
            {
                logger.LogInformation("No pending file found.");
            }

            for (var i = 0; i < pendingFileCount / syncSettings.FileBatchSize + 1; i++)
            {
                var batchId = i + 1;
                logger.LogInformation("Generating file batch {BatchId}...", batchId);

                var result = await fileStorageProvider.GetPendingFiles(commandOptions.DatabaseConnectionString, lastId, syncSettings.FileBatchSize);

                if (result.Count == 0)
                {
                    break;
                }

                lastId = result[^1].Id;

                var batchContent = string.Join("\r\n", result.Select(x =>
                {
                    var sourceFile = Path.Combine(commandOptions.SourceFolder, x.FileName);
                    var targetFile = Path.Combine(commandOptions.TargetFolder, x.FileName);

                    var command = $"COPY \"{sourceFile}\" \"{targetFile}\" /y";

                    return command;
                }));

                var batchFile = Path.Combine(batchRunFolder, $"{batchId}.bat");
                await File.WriteAllTextAsync(batchFile, batchContent);

                batchFiles.Add(batchFile);
                batchFileCount++;

                logger.LogInformation("Generated file batch {BatchId}: {BatchFile}, totally {BatchFileCount} batch files.", batchId, batchFile, batchFileCount);
            }

            logger.LogInformation("Generated {BatchFileCount} batch files.", batchFileCount);

            return batchFiles;
        }

        private async Task ProcessPendingFiles()
        {
            logger.LogInformation("Processing pending files...");

            var result = await fileStorageProvider.Run(syncSettings, commandOptions.DatabaseConnectionString, logger);
            
            syncReport.ProcessedFileCount = result.FileCount;
            syncReport.ProcessedFileBytes = result.FileBytes;

            logger.LogInformation("Processed pending files.");
        }

        private async Task ScanFiles(string rootFolder, SyncFileMode fileMode)
        {
            var pendingFiles = new List<FileDto>();
            var totalFileCount = 0L;

            logger.LogInformation("Scanning {FileMode} files...", fileMode);

            await fileStorageProvider.PrepareFileStorage(commandOptions.DatabaseConnectionString, fileMode, logger);

            foreach (var file in Directory.EnumerateFiles(rootFolder))
            {
                var fileInfo = new FileInfo(file);

                pendingFiles.Add(new FileDto
                {
                    FileName = fileInfo.FullName[(rootFolder.Length + 1)..],
                    Size = fileInfo.Length,
                    ModifiedTime = fileInfo.LastWriteTime
                });

                if (pendingFiles.Count % syncSettings.FileBatchSize == 0)
                {
                    await fileStorageProvider.Save(syncSettings, commandOptions.DatabaseConnectionString, pendingFiles, fileMode, logger);
                    pendingFiles.Clear();
                }

                totalFileCount++;
            }

            if (pendingFiles.Count > 0)
            {
                await fileStorageProvider.Save(syncSettings, commandOptions.DatabaseConnectionString, pendingFiles, fileMode, logger);
                pendingFiles.Clear();
            }

            logger.LogInformation("Scanned {FileMode} files.", fileMode);

            switch (fileMode)
            {
                case SyncFileMode.Source:
                    syncReport.SourceFileCount = totalFileCount;
                    break;
                case SyncFileMode.Target:
                    syncReport.TargetFileCount = totalFileCount;
                    break;
            }
        }
    }
}
