using CommandLine;
using SyncRoo.Utils;

namespace SyncRoo.Models
{
    public class CommandOptions
    {
        [Option('s', "Source", Required = false, HelpText = "The source folder where the files to be copied from.")]
        public string SourceFolder { get; set; }

        [Option('t', "Target", Required = false, HelpText = "The target folder where the files to be copied to.")]
        public string TargetFolder { get; set; }

        [Option('b', "Batch", Required = false, HelpText = "The intermediate folder for the file copy batch commands to be stored.")]
        public string BatchFolder { get; set; }

        [Option('f', "FilePatterns", Required = false, HelpText = "The file patterns to be serched for.")]
        public IEnumerable<string> FilePatterns { get; set; }

        [Option('o', "Operation", Required = false, HelpText = "A specific operation to be run rather than the whole sync process.")]
        public string Operation { get; set; }

        [Option('m', "MultiThreads", Required = false, HelpText = "The number of threads the process will use to concurrenctly copy the files.", Default = 1)]
        public int MultiThreads { get; set; }

        [Option('d', "Database", Required = false, HelpText = "The database connection string.")]
        public string DatabaseConnectionString { get; set; }

        [Option('r', "Rule", Required = false, HelpText = "The rule to determine whether a file should be copied or not: Standard, Newer, Larger.", Default = Rules.Standard)]
        public string Rule { get; set; }

        [Option('a', "AutoTeardown", Required = false, HelpText = "Automatically teardown intermediate resources.", Default = false)]
        public bool AutoTeardown { get; set; }

        [Option('n', "UsnJournal", Required = false, HelpText = "Use NTFS USN Journal to quickly search for files but this may use large volume of memory depending on the number of files on the drives.", Default = false)]
        public bool UsnJournal { get; set; }

        [Option('p', "Profile", Required = false, HelpText = "A profile file where you can define a series of source/target folders to be synced repeatedly.")]
        public string Profile { get; set; }
    }
}
