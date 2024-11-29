using SyncRoo.Models.Dtos;

namespace SyncRoo.Interfaces
{
    public interface IFileSourceProvider
    {
        string Name { get; }

        bool IsSupported(string folder, bool usnJournal);

        void Init();

        IEnumerable<FileInfo> Find(ScanTaskDto task);
    }
}
