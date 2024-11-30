using Microsoft.Extensions.Options;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Server.Handlers
{
    public class ScanHandler(IEnumerable<IFileSourceProvider> fileSourceProviders, IFileStorageProvider fileStorageProvider, IScanService scanService,
        IOptions<AppSyncSettings> syncSettings, IConfiguration configuration, ILogger<IReportProducer> logger)
    {
        private readonly string databaseConnectionString = configuration.GetConnectionString(ConnectionStrings.Database);

        public async Task<ScanResultDto> Run(ScanTaskDto scanTask)
        {
            var commandOptions = new CommandOptions
            {
                AutoTeardown = true
            };
            var syncReport = new SyncReport
            {
                StartedTime = DateTime.Now
            };

            var result = await scanService.Scan(scanTask, syncReport, fileStorageProvider, syncSettings.Value, fileSourceProviders, commandOptions, logger);

            return result;
        }

        public async Task<List<FileDto>> GetFiles(GetFileRequestDto request)
            => await fileStorageProvider.GetTargetFiles(databaseConnectionString, request.Page * request.Size, request.Size);

        public async Task Teardown()
            => await fileStorageProvider.Teardown(databaseConnectionString, logger);
    }
}
