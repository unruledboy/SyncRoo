namespace SyncRoo.Core.Models.Dtos
{
    public class SyncTaskDto
    {
        public string SourceFolder { get; set; }
        public string TargetFolder { get; set; }
        public string BatchFolder { get; set; }
        public string Rule { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<string> FilePatterns { get; set; } = [];
        public List<string> Limits { get; set; } = [];
    }
}
