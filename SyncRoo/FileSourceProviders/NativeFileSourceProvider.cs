using SyncRoo.Interfaces;
using SyncRoo.Models.Dtos;
using SyncRoo.Utils;

namespace SyncRoo.FileSourceProviders
{
    public class NativeFileSourceProvider : IFileSourceProvider
    {
        public string Name => SourceProviders.Native;

        public IEnumerable<FileInfo> Find(ScanTaskDto task)
        {
            foreach (var file in Directory.EnumerateFiles(task.RootFolder).Where(x => x.IsFilePatternMatched(task.FilePatterns)))
            {
                var fileInfo = new FileInfo(file);

                yield return fileInfo;
            }
        }

        public void Init()
        {
        }

        public bool IsSupported(string folder, bool usnJournal)
            => !usnJournal;
    }
}
