using Microsoft.Extensions.Logging;
using SyncRoo.Core.Interfaces;

namespace SyncRoo.Core.ReportProducers
{
    public class LogReportProducer(ILogger<IReportProducer> logger) : IReportProducer
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
