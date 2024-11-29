using System.IO.Enumeration;

namespace SyncRoo.Utils
{
    public static class FileCopyDecisioner
    {
        public static bool IsFilePatternMatched(this string file, List<string> filePatterns)
        {
            if (filePatterns == null
                || filePatterns.Count == 0
                || (filePatterns.Count == 1 && filePatterns[0] == FilePatterns.All)
                || filePatterns.Exists(x => FileSystemName.MatchesSimpleExpression(x, file)))
            {
                return true;
            }

            return false;
        }
    }
}
