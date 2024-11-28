namespace EverythingSZ.PInvoke.Constants
{
    public sealed class Win32ApiConstant
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        public const uint CREATE_NEW = 1;
        public const uint CREATE_ALWAYS = 2;
        public const uint OPEN_EXISTING = 3;
        public const uint OPEN_ALWAYS = 4;
        public const uint TRUNCATE_EXISTING = 5;

        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        public const int INVALID_HANDLE_VALUE = -1;

        public static int GWL_EXSTYLE = -20;
        public static int WS_EX_LAYERED = 0x00080000;
        public static int WS_EX_TRANSPARENT = 0x00000020;

        public const uint FSCTL_GET_OBJECT_ID = 0x9009c;
    }
}
