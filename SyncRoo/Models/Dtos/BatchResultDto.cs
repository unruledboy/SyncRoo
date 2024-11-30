namespace SyncRoo.Models.Dtos
{
    public class BatchResultDto
    {
        public List<string> Files { get; set; } = [];
        public string RunFolder { get; set; }
        public string BatchFolder { get; set; }
    }
}
