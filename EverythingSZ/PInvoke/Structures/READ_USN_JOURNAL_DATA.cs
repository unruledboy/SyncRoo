using System.Runtime.InteropServices;

namespace EverythingSZ.PInvoke.Structures
{
    /// <summary>
    /// Contains the Start USN(64bits), Reason Mask(32bits), Return Only on Close flag(32bits),
    /// Time Out(64bits), Bytes To Wait For(64bits), and USN Journal ID(64bits).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct READ_USN_JOURNAL_DATA
    {
        public long StartUsn;
        public uint ReasonMask;
        public uint ReturnOnlyOnClose;
        public ulong Timeout;
        public ulong bytesToWaitFor;
        public ulong UsnJournalId;
    }
}
