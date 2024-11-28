using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncRoo.Interfaces;
using SyncRoo.Models;
using SyncRoo.Models.Dtos;
using SyncRoo.Utils;

namespace SyncRoo.Core
{
    public class SyncEngine(CommandOptions commandOptions, IOptions<AppSyncSettings> syncSettings, IFileStorageProvider fileStorageProvider, IEnumerable<IFileSourceProvider> fileSourceProviders, ILogger logger)
    {
        private readonly AppSyncSettings syncSettings = syncSettings.Value;
        private readonly SyncReport syncReport = new();

        public async Task Sync()
        {
            syncReport.StartedTime = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(commandOptions.Profile))
            {
                await ProcessProfile();
            }
            else
            {
                var task = new SyncTaskDto
                {
                    SourceFolder = commandOptions.SourceFolder,
                    TargetFolder = commandOptions.TargetFolder,
                    BatchFolder = commandOptions.BatchFolder ?? syncSettings.BatchFolder
                };
                await ProcessTask(task, commandOptions.AutoTeardown);
            }

            syncReport.FinishedTime = DateTime.Now;

            ProduceReport();
        }

        private async Task ProcessProfile()
        {
            var result = ValidateProfile(out var profile);

            if (!result)
            {
                return;
            }

            foreach (var task in profile.Tasks)
            {
                await ProcessTask(task, true);
            }
        }

        private bool ValidateProfile(out ProfileDto profile)
        {
            profile = default;

            if (!File.Exists(commandOptions.Profile))
            {
                logger.LogError("Profile file does not exist.");

                return false;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            profile = JsonSerializer.Deserialize<ProfileDto>(File.ReadAllText(commandOptions.Profile), jsonOptions);

            if (profile == null)
            {
                logger.LogError("Invalid profile file.");

                return false;
            }

            if (profile.Tasks.Count == 0)
            {
                logger.LogError("No task found in the profile file.");

                return false;
            }

            var foldersDoNotExist = profile.Tasks.Where(x => string.IsNullOrWhiteSpace(x.SourceFolder) || !Directory.Exists(x.SourceFolder)).ToList();
            if (foldersDoNotExist.Count > 0)
            {
                foreach (var folder in foldersDoNotExist)
                {
                    logger.LogError("Source folder does exist: {RootFolder}", folder);
                }
                return false;
            }

            foreach (var task in profile.Tasks.Where(x => string.IsNullOrWhiteSpace(x.BatchFolder)))
            {
                task.BatchFolder = syncSettings.BatchFolder;
            }

            return true;
        }

        private async Task ProcessTask(SyncTaskDto task, bool autoTeardown)
        {
            await fileStorageProvider.Initialize(commandOptions.DatabaseConnectionString, logger);

            if (!string.IsNullOrWhiteSpace(commandOptions.Operation))
            {
                switch (commandOptions.Operation)
                {
                    case Operations.Scan:
                        await ScanFiles(task.SourceFolder, SyncFileMode.Source);
                        await ScanFiles(task.TargetFolder, SyncFileMode.Target);
                        break;
                    case Operations.Process:
                        await ProcessPendingFiles();
                        break;
                    case Operations.Run:
                        await GenerateAndRunBatchFiles(task);
                        break;
                }
            }
            else
            {
                await ScanFiles(task.SourceFolder, SyncFileMode.Source);
                await ScanFiles(task.TargetFolder, SyncFileMode.Target);
                await ProcessPendingFiles();
                await GenerateAndRunBatchFiles(task);
            }

            if (autoTeardown)
            {
                await fileStorageProvider.Teardown(commandOptions.DatabaseConnectionString, logger);
            }
        }

        private void ProduceReport()
        {
            syncReport.Timer.Stop();

            logger.LogInformation("Sync report");
            logger.LogInformation("\tStart time: {StartTime}", syncReport.StartedTime);
            logger.LogInformation("\tFinish time: {FinishTime}", syncReport.FinishedTime);
            logger.LogInformation("\tDuration: {Duration}", syncReport.Timer.Elapsed.ToReadableTimespan());
            logger.LogInformation("\tSource file count: {SourceFileCount}", syncReport.SourceFileCount);
            logger.LogInformation("\tTarget file count: {TargetFileCount}", syncReport.TargetFileCount);
            logger.LogInformation("\tProcessed file count: {ProcessedFileCount}", syncReport.ProcessedFileCount);
            logger.LogInformation("\tProcessed file size: {ProcessedFileSize}", FileUtils.FormatSize(syncReport.ProcessedFileBytes));

            var totalSeconds = syncReport.Timer.Elapsed.TotalSeconds;
            if (totalSeconds < 0)
            {
                totalSeconds = 1;
            }

            var bytesPerSecond = Convert.ToInt64(syncReport.ProcessedFileBytes / totalSeconds);
            var filesPerSecond = Convert.ToInt64(syncReport.ProcessedFileCount / totalSeconds);

            logger.LogInformation("\tProcess data speed: {ProcessSpeed}", $"{FileUtils.FormatSize(bytesPerSecond)}/s");
            logger.LogInformation("\tProcess file speed: {ProcessSpeed}", $"{filesPerSecond} files/s");
        }

        private async Task GenerateAndRunBatchFiles(SyncTaskDto task)
        {
            var batchResult = await GenerateBatchFiles(task);

            if (batchResult.Files.Count == 0)
            {
                logger.LogInformation("No batch files generated.");
            }

            Parallel.ForEach(batchResult.Files, new ParallelOptions
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

                    batchFile.SafeDeleteFile();

                    logger.LogInformation("Ran batch file {BatchFile}.", batchFile);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to run batch file {BatchFile}", batchFile);
                }
            });

            batchResult.Folder.SafeDeleteDirectory();
        }

        private async Task<BatchFileDto> GenerateBatchFiles(SyncTaskDto task)
        {
            const string DefaultBatchFolder = "Batch";
            using var connection = new SqlConnection(commandOptions.DatabaseConnectionString);
            var batchResult = new BatchFileDto();
            var batchFolder = task.BatchFolder;

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

            batchResult.Folder = batchRunFolder;

            logger.LogInformation("Batch files will be generated in {BatchFolder} for this run...", batchRunFolder);

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
                    var sourceFile = Path.Combine(task.SourceFolder, x.FileName);
                    var targetFile = Path.Combine(task.TargetFolder, x.FileName);

                    var command = $"COPY \"{sourceFile}\" \"{targetFile}\" /y";

                    return command;
                }));

                var batchFile = Path.Combine(batchRunFolder, $"{batchId}.bat");
                await File.WriteAllTextAsync(batchFile, batchContent);

                batchResult.Files.Add(batchFile);
                batchFileCount++;

                logger.LogInformation("Generated file batch {BatchId}: {BatchFile}, totally {BatchFileCount} batch files.", batchId, batchFile, batchFileCount);
            }

            logger.LogInformation("Generated {BatchFileCount} batch files.", batchFileCount);

            return batchResult;
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

            logger.LogInformation("Scanning {FileMode} files in {RootFolder}...", fileMode, rootFolder);

            if (!Directory.Exists(rootFolder))
            {
                Directory.CreateDirectory(rootFolder);

                logger.LogInformation("{RootFolder} does not exist. Created automatically.", rootFolder);

                return;
            }

            await fileStorageProvider.PrepareFileStorage(commandOptions.DatabaseConnectionString, fileMode, logger);

            var fileSource = GetFileSource(rootFolder);

            foreach (var fileInfo in fileSource)
            {
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

            switch (fileMode)
            {
                case SyncFileMode.Source:
                    syncReport.SourceFileCount = totalFileCount;
                    break;
                case SyncFileMode.Target:
                    syncReport.TargetFileCount = totalFileCount;
                    break;
            }

            logger.LogInformation("Scanned {FileMode} files in {RootFolder}...", fileMode, rootFolder);
        }

        private IEnumerable<FileInfo> GetFileSource(string folder)
        {
            logger.LogInformation("Initializing file source provider...");

            var fileSourceProvider = fileSourceProviders.FirstOrDefault(x => x.IsSupported(folder, commandOptions.UsnJournal));

            fileSourceProvider ??= fileSourceProviders.First(x => x.Name == SourceProviders.Native);
            fileSourceProvider.Init();

            logger.LogInformation("Initialized file source provider.");

            return fileSourceProvider.Find(folder);
        }
    }
}
