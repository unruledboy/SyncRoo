using System.Diagnostics;
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

        public static bool IsFileLimitMatched(this FileInfo fileInfo, List<string> limits)
        {
            if (limits == null
                || limits.Count == 0
                || limits.Exists(x => MatchFileLimit(x, fileInfo)))
            {
                return true;
            }

            return false;
        }

        public static bool IsValidFileLimit(this string limit, out string type, out object value)
        {
            const char Separator = '=';
            const int KB = 1024;
            var parts = limit.Split(Separator);

            type = default;
            value = default;

            if (parts.Length == 2)
            {
                type = limit.ToLowerInvariant();

                var isValidType = type switch
                {
                    LimitTypes.SizeMin or LimitTypes.SizeMax or LimitTypes.DateMin or LimitTypes.DateMax => true,
                    _ => false
                };

                if (isValidType)
                {
                    var valueText = parts[1].ToUpperInvariant(); 

                    switch (type)
                    {
                        case LimitTypes.SizeMin:
                        case LimitTypes.SizeMax:
                            var sizeMetric = valueText[^1];
                            var sizeFactor = sizeMetric switch
                            {
                                'K' => KB,
                                'M' => KB * KB,
                                'G' => KB * KB * KB,
                                _ => 1,
                            };

                            value = sizeFactor == 1 ? long.Parse(valueText) : long.Parse(valueText[..^1]);

                            return true;
                        case LimitTypes.DateMin:
                        case LimitTypes.DateMax:
                            var dateMetric = valueText[^1];
                            var now = DateTime.Now;
                            var duration = int.Parse(valueText[..^1]);

                            value = dateMetric switch
                            {
                                'Y' => now.AddYears(-duration),
                                'M' => now.AddMonths(-duration),
                                'W' => now.AddDays(-duration * 7),
                                'D' => now.AddDays(-duration),
                                'H' => now.AddHours(-duration),
                                'N' => now.AddMinutes(-duration),
                                'S' => now.AddSeconds(-duration),
                                _ => now
                            };

                            return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchFileLimit(string limit, FileInfo fileInfo)
        {
            if (limit.IsValidFileLimit(out var type, out var value))
            {
                switch (type)
                {
                    case LimitTypes.SizeMin:
                    case LimitTypes.SizeMax:
                        var size = (long)value;
                        
                        if (type == LimitTypes.SizeMin)
                            return fileInfo.Length >= size;
                        else
                            return fileInfo.Length <= size;
                    case LimitTypes.DateMin:
                    case LimitTypes.DateMax:
                        var date = (DateTime)value;

                        if (type == LimitTypes.DateMin)
                            return fileInfo.LastWriteTime >= date;
                        else
                            return fileInfo.LastWriteTime <= date;
                }
            }

            return false;
        }
    }
}
