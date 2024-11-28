using System.Runtime.InteropServices;

namespace EverythingSZ.PInvoke.Structures
{
    /// <summary>
    /// MFT Enum Data structure, contains Start File Reference Number(64bits), Low USN(64bits),
    /// High USN(64bits).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MFT_ENUM_DATA
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }
}
