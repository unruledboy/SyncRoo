namespace SyncRoo.Models.Dtos
{
    public class BatchFileDto
    {
        public List<string> Files { get; set; } = new List<string>();
        public string Folder { get; set; }
    }
}
