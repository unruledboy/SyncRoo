using EverythingSZ.QueryEngine;
using Microsoft.Extensions.Logging;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.FileSourceProviders
{
    public class NtfsUsnJournalFileSourceProvider(ILogger<IReportProducer> logger) : IFileSourceProvider
    {
        private readonly List<string> cachedFiles = [];
        private bool isInitialized = false;

        public string Name => SourceProviders.UsnJournal;

        public async IAsyncEnumerable<FileDto> Find(ScanTaskDto scanTask, AppSyncSettings syncSettings)
        {
            if (!Directory.Exists(scanTask.RootFolder))
            {
                Directory.CreateDirectory(scanTask.RootFolder);

                logger.LogInformation("{RootFolder} does not exist. Created automatically.", scanTask.RootFolder);

                yield break;
            }

            foreach (var file in cachedFiles.Where(x => x.StartsWith(scanTask.RootFolder, StringComparison.OrdinalIgnoreCase) && x.IsFilePatternMatched(scanTask.FilePatterns)))
            {
                var fileInfo = new FileInfo(file);

                if (!fileInfo.Exists)
                {
                    continue;
                }

                var fileDto = new FileDto
                {
                    FileName = fileInfo.FullName,
                    Size = fileInfo.Length,
                    ModifiedTime = fileInfo.LastWriteTime
                };

                if (fileDto.IsFileLimitMatched(scanTask.Limits))
                {
                    yield return await Task.FromResult(fileDto);
                }
            }
        }

        public void Init()
        {
            if (isInitialized)
            {
                return;
            }

            var result = Engine.GetAllFilesAndDirectories()
                .Where(x => x.IsFolder)
                .Select(x => x.FullFileName)
                .ToList();

            cachedFiles.AddRange(result);

            isInitialized = true;
        }

        public bool IsSupported(string folder, bool usnJournal)
        {
            if (folder.ValidateSyncProtocol(out _, out _))
            {
                return false;
            }

            if (SystemUtils.IsAdministrator() && usnJournal)
            {
                const string FileSystemNTFS = "NTFS";
                var driveName = Path.GetPathRoot(folder);

                if (!string.IsNullOrWhiteSpace(driveName))
                {
                    var driveInfo = new DriveInfo(driveName);
                    var isNTFS = driveInfo.DriveFormat.Equals(FileSystemNTFS, StringComparison.OrdinalIgnoreCase);

                    return isNTFS;
                }
            }

            return false;
        }
    }
}
