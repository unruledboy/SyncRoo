using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core
{
    public class SyncEngine(CommandOptions commandOptions, IOptions<AppSyncSettings> syncSettings, IFileStorageProvider fileStorageProvider, IEnumerable<IFileSourceProvider> fileSourceProviders,
        IEnumerable<IReportProducer> reportProducers, IScanService scanService, ILogger<IReportProducer> logger)
    {
        private readonly AppSyncSettings syncSettings = syncSettings.Value;
        private readonly SyncReport overallSyncReport = new();
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task Sync()
        {
            string batchFolder;

            overallSyncReport.StartedTime = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(commandOptions.Profile))
            {
                batchFolder = await ProcessProfile();
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

                var batchResult = await ProcessTask(task, commandOptions.AutoTeardown);
                batchFolder = batchResult.BatchFolder;
            }

            overallSyncReport.FinishedTime = DateTime.Now;

            await ProduceReport(overallSyncReport, batchFolder, ReportTypes.Summary);
        }

        private async Task<string> ProcessProfile()
        {
            var result = ValidateProfile(out var profile);

            if (!result)
            {
                return default;
            }

            string batchFolder = default;

            foreach (var task in profile.Tasks.Where(x => x.IsEnabled))
            {
                var batchResult = await ProcessTask(task, true);

                if (string.IsNullOrWhiteSpace(batchFolder))
                {
                    batchFolder = batchResult.BatchFolder;
                }
            }

            return batchFolder;
        }

        private bool ValidateProfile(out ProfileDto profile)
        {
            profile = default;

            if (!File.Exists(commandOptions.Profile))
            {
                logger.LogError("Profile file does not exist.");

                return false;
            }

            try
            {
                profile = JsonSerializer.Deserialize<ProfileDto>(File.ReadAllText(commandOptions.Profile), jsonOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invalid profile file: {ErrorMessage}", ex.Message);

                return false;
            }

            if (!profile.Tasks.Exists(x => x.IsEnabled))
            {
                logger.LogError("No active task found in the profile file.");

                return false;
            }

            var tasksWithSourceFoldersDoesNotExist = profile.Tasks.Where(x => x.IsEnabled
                    && (string.IsNullOrWhiteSpace(x.SourceFolder)
                        || (!x.SourceFolder.ValidateNetworkFolder(out _, out _) && !Directory.Exists(x.SourceFolder))))
                .ToList();
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

        private async Task<BatchResultDto> ProcessTask(SyncTaskDto task, bool autoTeardown)
        {
            var syncReport = new SyncReport
            {
                StartedTime = DateTime.Now,
                SourceFolder = task.SourceFolder,
                TargetFolder = task.TargetFolder
            };

            await fileStorageProvider.Initialize(commandOptions.DatabaseConnectionString, logger);

            BatchResultDto batchResult = default;

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
                        batchResult = await GenerateAndRunBatchFiles(task, syncReport);
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

                batchResult = await GenerateAndRunBatchFiles(task, syncReport);
            }

            if (autoTeardown)
            {
                await fileStorageProvider.Teardown(commandOptions.DatabaseConnectionString, logger);
            }

            syncReport.FinishedTime = DateTime.Now;
            syncReport.Timer.Stop();

            await ProduceReport(syncReport, batchResult?.BatchFolder, ReportTypes.Task);

            overallSyncReport.ProcessedFileBytes += syncReport.ProcessedFileBytes;
            overallSyncReport.ProcessedFileCount += syncReport.ProcessedFileCount;
            overallSyncReport.SourceFileCount += syncReport.SourceFileCount;
            overallSyncReport.TargetFileCount += syncReport.TargetFileCount;

            return batchResult;
        }

        private async Task ScanFiles(ScanTaskDto scanTask, SyncReport syncReport)
            => await scanService.Scan(scanTask, syncReport, fileStorageProvider, syncSettings, fileSourceProviders, commandOptions, logger);

        private async Task ProduceReport(SyncReport syncReport, string batchFolder, string reportType)
        {
            syncReport.Timer.Stop();

            var totalSeconds = syncReport.Timer.Elapsed.TotalSeconds;
            if (totalSeconds < 0)
            {
                totalSeconds = 1;
            }

            var bytesPerSecond = Convert.ToInt64(syncReport.ProcessedFileBytes / totalSeconds);
            var filesPerSecond = Convert.ToInt64(syncReport.ProcessedFileCount / totalSeconds);

            var items = new List<string>
            {
                $"Start time: {syncReport.StartedTime}",
                $"Finish time: {syncReport.FinishedTime}",
                $"Duration: {syncReport.Timer.Elapsed.ToReadableTimespan()}",
                $"Source file count: {syncReport.SourceFileCount}",
                $"Target file count: {syncReport.TargetFileCount}",
                $"Processed file count: {syncReport.ProcessedFileCount}",
                $"Processed file size: {FileUtils.FormatSize(syncReport.ProcessedFileBytes)}",
                $"Process data speed: {FileUtils.FormatSize(bytesPerSecond)}/s",
                $"Process file speed: {filesPerSecond} files/s"
            };

            if (!string.IsNullOrWhiteSpace(syncReport.SourceFolder))
            {
                items.Insert(0, $"Source Folder: {syncReport.SourceFolder}");
                items.Insert(1, $"Target Folder: {syncReport.TargetFolder}");
            }

            foreach (var reportProducer in reportProducers)
            {
                await reportProducer.Write(overallSyncReport.StartedTime, reportType, batchFolder, items);
            }
        }

        private async Task<BatchResultDto> GenerateAndRunBatchFiles(SyncTaskDto task, SyncReport syncReport)
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

            batchResult.RunFolder.SafeDeleteDirectory();

            logger.LogInformation("Cleaned up batch folder {BatchFolder}.", batchResult.RunFolder);

            return batchResult;
        }

        private async Task<BatchResultDto> GenerateBatchFiles(SyncTaskDto task, SyncReport syncReport)
        {
            const string DefaultBatchFolder = "Batch";
            using var connection = new SqlConnection(commandOptions.DatabaseConnectionString);
            var batchResult = new BatchResultDto();
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

            batchResult.BatchFolder = batchFolder;
            batchResult.RunFolder = batchRunFolder;

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

                if (!task.SourceFolder.ValidateNetworkFolder(out _, out var sourceFolder))
                {
                    sourceFolder = task.SourceFolder;
                }

                if (!task.TargetFolder.ValidateNetworkFolder(out _, out var targetFolder))
                {
                    targetFolder = task.TargetFolder;
                }

                var batchContent = new StringBuilder();
                batchContent.AppendLine("chcp 65001");
                batchContent.AppendLine(string.Join("\r\n", result.Select(x =>
                {
                    var sourceFile = Path.Combine(sourceFolder, x.FileName);
                    var targetFile = Path.Combine(targetFolder, x.FileName);
                    var command = $"COPY \"{sourceFile}\" \"{targetFile}\" /y";

                    return command;
                })));

                var batchFile = Path.Combine(batchRunFolder, $"{batchId}.bat");
                await File.WriteAllTextAsync(batchFile, batchContent.ToString());

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
    }
}
