using System.Runtime.InteropServices;

namespace EverythingSZ.PInvoke.Structures
{
    /// <summary>
    /// USN Journal Data structure, contains USN Journal ID(64bits), First USN(64bits), Next USN(64bits),
    /// Lowest Valid USN(64bits), Max USN(64bits), Maximum Size(64bits) and Allocation Delta(64bits).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct USN_JOURNAL_DATA
    {
        public ulong UsnJournalID;
        public long FirstUsn;
        public long NextUsn;
        public long LowestValidUsn;
        public long MaxUsn;
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }
}
