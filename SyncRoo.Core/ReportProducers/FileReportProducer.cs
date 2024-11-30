using System.Text;
using SyncRoo.Core.Interfaces;

namespace SyncRoo.Core.ReportProducers
{
    public class FileReportProducer : IReportProducer
    {
        public async Task Write(DateTime startedTime, string reportType, string batchFolder, List<string> items)
        {
            if (string.IsNullOrWhiteSpace(batchFolder))
            {
                return;
            }

            var output = new StringBuilder();

            output.AppendLine();

            output.AppendFormat("{0} report{1}", reportType, Environment.NewLine);

            foreach (var item in items)
            {
                output.AppendFormat("\t{0}{1}", item, Environment.NewLine);
            }

            output.AppendLine("");
            output.AppendLine("");

            var fileName = startedTime.ToString("yyyy-MM-dd HHmmss") + ".txt";
            var fullFileName = Path.Combine(batchFolder, fileName);

            await File.AppendAllTextAsync(fullFileName, output.ToString());
        }
    }
}
