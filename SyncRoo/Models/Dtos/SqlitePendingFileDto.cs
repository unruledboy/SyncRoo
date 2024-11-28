namespace SyncRoo.Models.Dtos
{
    public class SqlitePendingFileDto
    {
        public long Id { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public long ModifiedTime { get; set; }
    }
}
