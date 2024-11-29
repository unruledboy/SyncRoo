namespace SyncRoo.Models.Dtos
{
    public class SyncTaskDto
    {
        public string SourceFolder { get; set; }
        public string TargetFolder { get; set; }
        public string BatchFolder { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<string> FilePatterns { get; set; } = [];
    }
}
