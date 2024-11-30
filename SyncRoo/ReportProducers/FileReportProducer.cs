﻿using System.Text;
using SyncRoo.Interfaces;

namespace SyncRoo.ReportProducers
{
    public class FileReportProducer : IReportProducer
    {
        public async Task Write(DateTime startedTime, string reportType, string batchFolder, List<string> items)
        {
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
