namespace SyncRoo.Utils
{
    public static class OptionSets
    {
        public const string CommandLine = "command";
        public const string Profile = "profile";
    }

    public static class FilePatterns
    {
        public const string All = "*.*";
    }

    public static class ReportTypes
    {
        public const string Current = "Current";
        public const string Overall = "Overall";
    }

    public static class Rules
    {
        public const string Standard = "standard";
        public const string Newer = "newer";
        public const string Larger = "larger";
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
        public const string InMemory = "inmemory";
    }

    public static class SourceProviders
    {
        public const string Native = "Native";
        public const string UsnJournal = "USN";
    }

    public static class LimitTypes
    {
        public const string SizeMin = "sizemin";
        public const string SizeMax = "sizemax";
        public const string DateMin = "datemin";
        public const string DateMax = "datemax";
    }

    public enum SyncFileMode
    {
        None = 0,
        Source = 1,
        Target = 2,
        Pending = 3
    }
}
