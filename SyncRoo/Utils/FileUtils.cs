
using System.Globalization;

namespace SyncRoo.Utils
{
    public static class FileUtils
    {
        public static void SafeDeleteFile(this string file)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        public static void SafeDeleteDirectory(this string folder)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder);
            }
        }

        public static string FormatSize(this long value)
        {
            string suffix;
            double readable;

            switch (Math.Abs(value))
            {
                case >= 0x1000000000000000:
                    suffix = "EiB";
                    readable = value >> 50;
                    break;
                case >= 0x4000000000000:
                    suffix = "PiB";
                    readable = value >> 40;
                    break;
                case >= 0x10000000000:
                    suffix = "TiB";
                    readable = value >> 30;
                    break;
                case >= 0x40000000:
                    suffix = "GiB";
                    readable = value >> 20;
                    break;
                case >= 0x100000:
                    suffix = "MiB";
                    readable = value >> 10;
                    break;
                case >= 0x400:
                    suffix = "KiB";
                    readable = value;
                    break;
                default:
                    return value.ToString("0 B");
            }

            return (readable / 1024).ToString("0.## ", CultureInfo.InvariantCulture) + suffix;
        }
    }
}
