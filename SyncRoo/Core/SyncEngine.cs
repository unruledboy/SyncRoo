using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
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
        private readonly SyncReport overallSyncReport = new();
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task Sync()
        {
            overallSyncReport.StartedTime = DateTime.Now;

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
                    BatchFolder = commandOptions.BatchFolder ?? syncSettings.BatchFolder,
                    FilePatterns = commandOptions.FilePatterns?.ToList() ?? [],
                    Rule = commandOptions.Rule,
                    Limits = commandOptions.Limits?.ToList() ?? [],
                };
                await ProcessTask(task, commandOptions.AutoTeardown);
            }

            overallSyncReport.FinishedTime = DateTime.Now;
            
            ProduceReport(overallSyncReport, ReportTypes.Overall);
        }

        private async Task ProcessProfile()
        {
            var result = ValidateProfile(out var profile);

            if (!result)
            {
                return;
            }

            foreach (var task in profile.Tasks.Where(x => x.IsEnabled))
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

            profile = JsonSerializer.Deserialize<ProfileDto>(File.ReadAllText(commandOptions.Profile), jsonOptions);

            if (profile == null)
            {
                logger.LogError("Invalid profile file.");

                return false;
            }

            if (!profile.Tasks.Exists(x => x.IsEnabled))
            {
                logger.LogError("No active task found in the profile file.");

                return false;
            }

            var tasksWithSourceFoldersDoesNotExist = profile.Tasks.Where(x => x.IsEnabled && string.IsNullOrWhiteSpace(x.SourceFolder) || !Directory.Exists(x.SourceFolder)).ToList();
            if (tasksWithSourceFoldersDoesNotExist.Count > 0)
            {
                foreach (var task in tasksWithSourceFoldersDoesNotExist)
                {
                    logger.LogError("Task with source folder does exist: {RootFolder}", task.SourceFolder);
                }

                return false;
            }

            foreach (var task in profile.Tasks.Where(x => x.IsEnabled && string.IsNullOrWhiteSpace(x.BatchFolder)))
            {
                task.BatchFolder = syncSettings.BatchFolder;
            }

            foreach (var task in profile.Tasks.Where(x => string.IsNullOrWhiteSpace(x.Rule)))
            {
                task.Rule ??= Rules.Standard;
            }

            var tasksWithInvalidRule = profile.Tasks.Where(x => x.IsEnabled && x.Rule != Rules.Standard && x.Rule != Rules.Newer && x.Rule != Rules.Larger).ToList();
            if (tasksWithInvalidRule.Count > 0)
            {
                foreach (var task in tasksWithInvalidRule)
                {
                    logger.LogError("Task with invalid rule: {Rule}, source folder: {RootFolder}", task.Rule, task.SourceFolder);
                }

                return false;
            }


            var tasksWithInvalidLimit = profile.Tasks.Where(x => x.IsEnabled && x.Limits != null && x.Limits.Exists(l => !l.IsValidFileLimit(out _, out _))).ToList();
            if (tasksWithInvalidLimit.Count > 0)
            {
                foreach (var task in tasksWithInvalidLimit)
                {
                    logger.LogError("Task with invalid limit: {Limit}, source folder: {RootFolder}", string.Join(',', task.Limits), task.SourceFolder);
                }
                return false;
            }

            return true;
        }

        private async Task ProcessTask(SyncTaskDto task, bool autoTeardown)
        {
            var syncReport = new SyncReport
            {
                StartedTime = DateTime.Now
            };

            await fileStorageProvider.Initialize(commandOptions.DatabaseConnectionString, logger);

            if (!string.IsNullOrWhiteSpace(commandOptions.Operation))
            {
                switch (commandOptions.Operation)
                {
                    case Operations.Scan:
                        await ScanFiles(new ScanTaskDto
                        {
                            RootFolder = task.SourceFolder,
                            FileMode = SyncFileMode.Source,
                            FilePatterns = task.FilePatterns,
                            Rule = task.Rule,
                            Limits = task.Limits
                        }, syncReport);

                        await ScanFiles(new ScanTaskDto
                        {
                            RootFolder = task.TargetFolder,
                            FileMode = SyncFileMode.Target,
                            FilePatterns = task.FilePatterns,
                            Rule = task.Rule,
                            Limits = task.Limits
                        }, syncReport);
                        break;
                    case Operations.Process:
                        await ProcessPendingFiles(task);
                        break;
                    case Operations.Run:
                        await GenerateAndRunBatchFiles(task, syncReport);
                        break;
                }
            }
            else
            {
                await ScanFiles(new ScanTaskDto
                {
                    RootFolder = task.SourceFolder,
                    FileMode = SyncFileMode.Source,
                    FilePatterns = task.FilePatterns,
                    Rule = task.Rule,
                    Limits = task.Limits
                }, syncReport);

                await ScanFiles(new ScanTaskDto
                {
                    RootFolder = task.TargetFolder,
                    FileMode = SyncFileMode.Target,
                    FilePatterns = task.FilePatterns,
                    Rule = task.Rule,
                    Limits = task.Limits
                }, syncReport);

                await ProcessPendingFiles(task);

                await GenerateAndRunBatchFiles(task, syncReport);
            }

            if (autoTeardown)
            {
                await fileStorageProvider.Teardown(commandOptions.DatabaseConnectionString, logger);
            }

            syncReport.FinishedTime = DateTime.Now;
            syncReport.Timer.Stop();

            ProduceReport(syncReport, ReportTypes.Current);

            overallSyncReport.ProcessedFileBytes += syncReport.ProcessedFileBytes;
            overallSyncReport.ProcessedFileCount += syncReport.ProcessedFileCount;
            overallSyncReport.SourceFileCount += syncReport.SourceFileCount;
            overallSyncReport.TargetFileCount += syncReport.TargetFileCount;
        }

        private void ProduceReport(SyncReport syncReport, string reportType)
        {
            syncReport.Timer.Stop();

            logger.LogInformation("");
            logger.LogInformation("{ReportType} report", reportType);
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
            logger.LogInformation("");
            logger.LogInformation("");
        }

        private async Task GenerateAndRunBatchFiles(SyncTaskDto task, SyncReport syncReport)
        {
            var batchResult = await GenerateBatchFiles(task, syncReport);

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

        private async Task<BatchFileDto> GenerateBatchFiles(SyncTaskDto task, SyncReport syncReport)
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

                syncReport.ProcessedFileBytes += result.Sum(x => x.Size);
                syncReport.ProcessedFileCount += result.Count;

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

                logger.LogInformation("Generated file batch {BatchId}: {BatchFile}, totally {BatchFileCount} batch files.", batchId, batchFile, batchResult.Files.Count);
            }

            logger.LogInformation("Generated {BatchFileCount} batch files.", batchResult.Files.Count);

            return batchResult;
        }

        private async Task ProcessPendingFiles(SyncTaskDto task)
        {
            logger.LogInformation("Processing pending files...");

            await fileStorageProvider.Run(syncSettings, commandOptions.DatabaseConnectionString, task, logger);

            logger.LogInformation("Processed pending files.");
        }

        private async Task ScanFiles(ScanTaskDto scanTask, SyncReport syncReport)
        {
            var pendingFiles = new List<FileDto>();
            var totalFileCount = 0L;

            logger.LogInformation("Scanning {FileMode} files in {RootFolder}...", scanTask.FileMode, scanTask.RootFolder);

            if (!Directory.Exists(scanTask.RootFolder))
            {
                Directory.CreateDirectory(scanTask.RootFolder);

                logger.LogInformation("{RootFolder} does not exist. Created automatically.", scanTask.RootFolder);

                return;
            }

            await fileStorageProvider.PrepareFileStorage(commandOptions.DatabaseConnectionString, scanTask.FileMode, logger);

            var fileSource = GetFileSource(scanTask);

            foreach (var fileInfo in fileSource)
            {
                pendingFiles.Add(new FileDto
                {
                    FileName = fileInfo.FullName[(scanTask.RootFolder.Length + 1)..],
                    Size = fileInfo.Length,
                    ModifiedTime = fileInfo.LastWriteTime
                });

                totalFileCount++;

                if (pendingFiles.Count % syncSettings.FileBatchSize == 0)
                {
                    await fileStorageProvider.Save(syncSettings, commandOptions.DatabaseConnectionString, totalFileCount, pendingFiles, scanTask.FileMode, logger);
                    pendingFiles.Clear();
                }
            }

            if (pendingFiles.Count > 0)
            {
                await fileStorageProvider.Save(syncSettings, commandOptions.DatabaseConnectionString, totalFileCount, pendingFiles, scanTask.FileMode, logger);
                pendingFiles.Clear();
            }

            switch (scanTask.FileMode)
            {
                case SyncFileMode.Source:
                    syncReport.SourceFileCount = totalFileCount;
                    break;
                case SyncFileMode.Target:
                    syncReport.TargetFileCount = totalFileCount;
                    break;
            }

            logger.LogInformation("Scanned {FileMode} and found {FileCount} files in {RootFolder}.", scanTask.FileMode, totalFileCount, scanTask.RootFolder);
        }

        private IEnumerable<FileInfo> GetFileSource(ScanTaskDto scanTask)
        {
            logger.LogInformation("Initializing file source provider...");

            var fileSourceProvider = fileSourceProviders.FirstOrDefault(x => x.IsSupported(scanTask.RootFolder, commandOptions.UsnJournal));

            fileSourceProvider ??= fileSourceProviders.First(x => x.Name == SourceProviders.Native);
            fileSourceProvider.Init();

            logger.LogInformation("Initialized file source provider. Searching for files...");

            return fileSourceProvider.Find(scanTask);
        }
    }
}
