using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.FileSourceProviders
{
    public class NativeFileSourceProvider : IFileSourceProvider
    {
        public string Name => SourceProviders.Native;

        public IEnumerable<FileInfo> Find(ScanTaskDto scanTask)
        {
            foreach (var file in Directory.EnumerateFiles(scanTask.RootFolder).Where(x => x.IsFilePatternMatched(scanTask.FilePatterns)))
            {
                var fileInfo = new FileInfo(file);

                if (fileInfo.IsFileLimitMatched(scanTask.Limits))
                {
                    yield return fileInfo;
                }
            }
        }

        public void Init()
        {
        }

        public bool IsSupported(string folder, bool usnJournal)
            => !usnJournal;
    }
}
