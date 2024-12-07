using Microsoft.Extensions.Logging;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.Services
{
    public class FileSystemScanService : IScanService
    {
        public async Task<ScanResultDto> Scan(ScanTaskDto scanTask, SyncReport syncReport, IFileStorageProvider fileStorageProvider, AppSyncSettings syncSettings,
            IEnumerable<IFileSourceProvider> fileSourceProviders, CommandOptions commandOptions, ILogger logger)
        {
            var pendingFiles = new List<FileDto>();
            var totalFileCount = 0L;

            logger.LogInformation("Scanning {FileMode} files in {RootFolder}...", scanTask.FileMode, scanTask.RootFolder);

            await fileStorageProvider.PrepareFileStorage(commandOptions.DatabaseConnectionString, scanTask.FileMode, logger);

            var fileSource = GetFileSource(scanTask, fileSourceProviders, syncSettings, logger);

            if (!scanTask.RootFolder.ValidateSyncProtocol(out _, out var rootFolder))
            {
                rootFolder = scanTask.RootFolder;
            }

            await foreach (var fileInfo in fileSource)
            {
                pendingFiles.Add(new FileDto
                {
                    FileName = fileInfo.FileName.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase) ? fileInfo.FileName[(rootFolder.Length + 1)..] : fileInfo.FileName,
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

        private static IAsyncEnumerable<FileDto> GetFileSource(ScanTaskDto scanTask, IEnumerable<IFileSourceProvider> fileSourceProviders, AppSyncSettings syncSettings, ILogger logger)
        {
            logger.LogInformation("Initializing file source provider...");

            var fileSourceProvider = fileSourceProviders.FirstOrDefault(x => x.IsSupported(scanTask.RootFolder, scanTask.UsnJournal));

            fileSourceProvider ??= fileSourceProviders.First(x => x.Name == SourceProviders.Native);
            fileSourceProvider.Init();

            logger.LogInformation("Initialized file source provider. Searching for files...");

            return fileSourceProvider.Find(scanTask, syncSettings);
        }
    }
}
