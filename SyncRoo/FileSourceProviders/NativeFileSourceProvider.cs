using SyncRoo.Interfaces;
using SyncRoo.Utils;

namespace SyncRoo.FileSourceProviders
{
    public class NativeFileSourceProvider : IFileSourceProvider
    {
        public string Name => SourceProviders.Native;

        public IEnumerable<FileInfo> Find(string folder)
        {
            foreach (var file in Directory.EnumerateFiles(folder))
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
