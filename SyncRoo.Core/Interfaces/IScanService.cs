using Microsoft.Extensions.Logging;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;

namespace SyncRoo.Core.Interfaces
{
    public interface IScanService
    {
        Task<ScanResultDto> Scan(ScanTaskDto scanTask, SyncReport syncReport, IFileStorageProvider fileStorageProvider, AppSyncSettings syncSettings,
            IEnumerable<IFileSourceProvider> fileSourceProviders, CommandOptions commandOptions, ILogger logger);
    }
}
