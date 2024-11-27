using System.Diagnostics;

namespace SyncRoo.Models
{
    public class SyncReport
    {
        public Stopwatch Timer { get; } = Stopwatch.StartNew();

        public DateTime StartedTime { get; set; }
        public DateTime FinishedTime { get; set; }
        public long SourceFileCount { get; set; }
        public long TargetFileCount { get; set; }
        public long ProcessedFileCount { get; set; }
        public long ProcessedFileBytes { get; set; }

    }
}
