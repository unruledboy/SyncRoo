using EverythingSZ.QueryEngine;
using SyncRoo.Interfaces;
using SyncRoo.Utils;

namespace SyncRoo.FileSourceProviders
{
    public class NtfsUsnJournalFileSourceProvider : IFileSourceProvider
    {
        private readonly List<string> cachedFiles = [];
        private bool isInitialized = false;

        public string Name => SourceProviders.UsnJournal;

        public IEnumerable<FileInfo> Find(string folder)
        {
            foreach (var file in cachedFiles.Where(x => x.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new FileInfo(file);
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
