using Microsoft.Extensions.Logging;
using SyncRoo.Models;
using SyncRoo.Models.Dtos;
using SyncRoo.Utils;

namespace SyncRoo.Interfaces
{
    public interface IFileStorageProvider
    {
        Task Initialize(string connectionString, ILogger logger);

        Task Teardown(string connectionString, ILogger logger);

        Task PrepareFileStorage(string connectionString, SyncFileMode fileMode, ILogger logger);

        Task Save(AppSyncSettings syncSettings, string connectionString, List<FileDto> files, SyncFileMode fileMode, ILogger logger);

        Task Run(AppSyncSettings syncSettings, string connectionString, ILogger logger);

        Task<long> GetPendingFileCount(string connectionString);

        Task<List<PendingFileDto>> GetPendingFiles(string connectionString, long lastId, int batchSize);

    }
}
