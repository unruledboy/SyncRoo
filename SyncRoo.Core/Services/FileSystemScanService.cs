using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Models;
using SyncRoo.Core.Utils;
using Microsoft.Extensions.Logging;

namespace SyncRoo.Core.Services
{
    public class FileSystemScanService : IScanService
    {
        public async Task<ScanResultDto> Scan(ScanTaskDto scanTask, SyncReport syncReport, IFileStorageProvider fileStorageProvider, AppSyncSettings syncSettings,
            IEnumerable<IFileSourceProvider> fileSourceProviders, CommandOptions commandOptions, ILogger<IReportProducer> logger)
        {
            var pendingFiles = new List<FileDto>();
            var totalFileCount = 0L;

            logger.LogInformation("Scanning {FileMode} files in {RootFolder}...", scanTask.FileMode, scanTask.RootFolder);

            if (!Directory.Exists(scanTask.RootFolder))
            {
                Directory.CreateDirectory(scanTask.RootFolder);

                logger.LogInformation("{RootFolder} does not exist. Created automatically.", scanTask.RootFolder);

                return new ScanResultDto
                {
                    FileCount = 0
                };
            }

            await fileStorageProvider.PrepareFileStorage(commandOptions.DatabaseConnectionString, scanTask.FileMode, logger);

            var fileSource = GetFileSource(scanTask, fileSourceProviders, commandOptions, syncSettings, logger);

            await foreach (var fileInfo in fileSource)
            {
                pendingFiles.Add(new FileDto
                {
                    FileName = fileInfo.FileName[(scanTask.RootFolder.Length + 1)..],
                    Size = fileInfo.Size,
                    ModifiedTime = fileInfo.ModifiedTime
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

            return new ScanResultDto
            {
                FileCount = totalFileCount
            };
        }

        private static IAsyncEnumerable<FileDto> GetFileSource(ScanTaskDto scanTask, IEnumerable<IFileSourceProvider> fileSourceProviders, CommandOptions commandOptions, AppSyncSettings syncSettings, ILogger<IReportProducer> logger)
        {
            logger.LogInformation("Initializing file source provider...");

            var fileSourceProvider = fileSourceProviders.FirstOrDefault(x => x.IsSupported(scanTask.RootFolder, commandOptions.UsnJournal));

            fileSourceProvider ??= fileSourceProviders.First(x => x.Name == SourceProviders.Native);
            fileSourceProvider.Init();

            logger.LogInformation("Initialized file source provider. Searching for files...");

            return fileSourceProvider.Find(scanTask, syncSettings);
        }
    }
}
