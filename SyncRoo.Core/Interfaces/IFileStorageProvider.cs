using Microsoft.Extensions.Logging;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.Interfaces
{
    public interface IFileStorageProvider
    {
        Task Initialize(string connectionString, ILogger logger);

        Task Teardown(string connectionString, ILogger logger);

        Task PrepareFileStorage(string connectionString, SyncFileMode fileMode, ILogger logger);

        Task Save(AppSyncSettings syncSettings, string connectionString, long runtimeTotal, List<FileDto> files, SyncFileMode fileMode, ILogger logger);

        Task Run(AppSyncSettings syncSettings, string connectionString, SyncTaskDto task, ILogger logger);

        Task<long> GetPendingFileCount(string connectionString);

        Task<List<PendingFileDto>> GetPendingFiles(string connectionString, long lastId, int batchSize);

        Task<List<FileDto>> GetSourceFiles(string connectionString, long lastId, int batchSize);

        Task<List<FileDto>> GetTargetFiles(string connectionString, long lastId, int batchSize);
    }
}
