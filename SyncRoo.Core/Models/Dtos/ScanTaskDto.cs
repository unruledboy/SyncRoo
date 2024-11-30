using SyncRoo.Core.Utils;

namespace SyncRoo.Core.Models.Dtos
{
    public class ScanTaskDto
    {
        public string RootFolder { get; set; }
        public List<string> FilePatterns { get; set; }
        public string Rule { get; set; }
        public List<string> Limits { get; set; } = [];
        public SyncFileMode FileMode { get; set; }
    }
}
