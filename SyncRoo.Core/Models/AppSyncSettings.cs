namespace SyncRoo.Core.Models
{
    public class AppSyncSettings
    {
        public string BatchFolder { get; set; }
        public int OperationDelayInMs { get; set; }
        public int CommandTimeoutInSeconds { get; set; }
        public int ProcessTimeoutInSeconds { get; set; }
        public int FileBatchSize { get; set; }
    }
}
