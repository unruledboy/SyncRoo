using System.Runtime.InteropServices;

namespace EverythingSZ.PInvoke.Structures
{
    /// <summary>
    /// Create USN Journal Data structure, contains Maximum Size(64bits) and Allocation Delta(64(bits).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CREATE_USN_JOURNAL_DATA
    {
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }
}
