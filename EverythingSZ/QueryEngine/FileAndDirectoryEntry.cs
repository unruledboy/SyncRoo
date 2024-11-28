using EverythingSZ.UsnOperation;

namespace EverythingSZ.QueryEngine
{
    public class FileAndDirectoryEntry
    {
        public string FileName { get; }

        public string Path { get; }

        public string FullFileName
            => string.Concat(Path, "\\", FileName);

        public bool IsFolder { get; }

        public FileAndDirectoryEntry(UsnEntry usnEntry, string path)
        {
            FileName = usnEntry.FileName;
            IsFolder = usnEntry.IsFolder;
            Path = path;
        }
    }
}
