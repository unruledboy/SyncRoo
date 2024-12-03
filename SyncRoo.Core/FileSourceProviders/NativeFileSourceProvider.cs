using Microsoft.Extensions.Logging;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.FileSourceProviders
{
    public class NativeFileSourceProvider(ILogger<IReportProducer> logger) : IFileSourceProvider
    {
        public string Name => SourceProviders.Native;

        public async IAsyncEnumerable<FileDto> Find(ScanTaskDto scanTask, AppSyncSettings syncSettings)
        {
            if (!Directory.Exists(scanTask.RootFolder))
            {
                Directory.CreateDirectory(scanTask.RootFolder);

                logger.LogInformation("{RootFolder} does not exist. Created automatically.", scanTask.RootFolder);

                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(scanTask.RootFolder, FilePatterns.All, SearchOption.AllDirectories)
                .Where(x => x.IsFilePatternMatched(scanTask.FilePatterns)))
            {
                var fileInfo = new FileInfo(file);

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
        }

        public bool IsSupported(string folder, bool usnJournal)
        {
            if (folder.ValidateNetworkFolder(out _, out _))
            {
                return false;
            }

            return !usnJournal;
        }
    }
}
