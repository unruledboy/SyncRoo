using SyncRoo.Core.Utils;

namespace SyncRoo.Core.Models.Dtos
{
    public class GetFileRequestDto
    {
        public SyncFileMode FileMode { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }
}
