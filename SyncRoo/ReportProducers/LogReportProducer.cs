using Microsoft.Extensions.Logging;
using SyncRoo.Core;
using SyncRoo.Interfaces;

namespace SyncRoo.ReportProducers
{
    public class LogReportProducer(ILogger<SyncEngine> logger) : IReportProducer
    {
        public async Task Write(DateTime startedTime, string reportType, string batchFolder, List<string> items)
        {
            logger.LogInformation("");

            logger.LogInformation("{ReportType} report", reportType);

            foreach (var item in items)
            {
                logger.LogInformation("\t{LogItem}", item);
            }

            logger.LogInformation("");
            logger.LogInformation("");

            await Task.CompletedTask;
        }
    }
}
