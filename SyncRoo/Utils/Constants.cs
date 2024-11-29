namespace SyncRoo.Utils
{
    public static class FilePatterns
    {
        public const string All = "*.*";
    }

    public static class Operations
    {
        public const string Scan = "scan";
        public const string Process = "process";
        public const string Run = "run";
    }

    public static class StorageProviders
    {
        public const string SqlServer = "sqlserver";
        public const string SqlServerLocalDB = "localdb";
        public const string Sqlite = "sqlite";
    }

    public static class SourceProviders
    {
        public const string Native = "Native";
        public const string UsnJournal = "USN";
    }

    public enum SyncFileMode
    {
        None = 0,
        Source = 1,
        Target = 2,
        Pending = 3
    }
}
