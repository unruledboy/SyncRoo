using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;

namespace SyncRoo.Core.Interfaces
{
    public interface IFileSourceProvider
    {
        string Name { get; }

        bool IsSupported(string folder, bool usnJournal);

        void Init();

        IAsyncEnumerable<FileDto> Find(ScanTaskDto scanTask, AppSyncSettings syncSettings);
    }
}
