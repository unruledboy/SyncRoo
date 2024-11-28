using System;
using System.Runtime.InteropServices;

namespace EverythingSZ.PInvoke.Structures
{
    /// <summary>
    /// Contains the USN Record Length(32bits), USN(64bits), File Reference Number(64bits), 
    /// Parent File Reference Number(64bits), Reason Code(32bits), File Attributes(32bits),
    /// File Name Length(32bits), the File Name Offset(32bits) and the File Name.
    /// </summary>
    public class USN_RECORD_V2
    {
        private const int FR_OFFSET = 8;
        private const int PFR_OFFSET = 16;
        private const int USN_OFFSET = 24;
        private const int REASON_OFFSET = 40;
        private const int FA_OFFSET = 52;
        private const int FNL_OFFSET = 56;
        private const int FN_OFFSET = 58;

        public uint RecordLength { get; private set; }
        public ulong FileReferenceNumber { get; private set; }
        public ulong ParentFileReferenceNumber { get; private set; }
        public long Usn { get; private set; }
        public uint Reason { get; private set; }
        public uint FileAttributes { get; private set; }
        public int FileNameLength { get; private set; }
        public int FileNameOffset { get; private set; }
        public string FileName { get; private set; }

        /// <summary>
        /// USN Record Constructor
        /// </summary>
        /// <param name="usnRecordPtr">Buffer of bytes representing the USN Record</param>
        public USN_RECORD_V2(IntPtr usnRecordPtr)
        {
            RecordLength = (uint)Marshal.ReadInt32(usnRecordPtr);
            FileReferenceNumber = (ulong)Marshal.ReadInt64(usnRecordPtr, FR_OFFSET);
            ParentFileReferenceNumber = (ulong)Marshal.ReadInt64(usnRecordPtr, PFR_OFFSET);
            Usn = Marshal.ReadInt64(usnRecordPtr, USN_OFFSET);
            Reason = (uint)Marshal.ReadInt32(usnRecordPtr, REASON_OFFSET);
            FileAttributes = (uint)Marshal.ReadInt32(usnRecordPtr, FA_OFFSET);
            FileNameLength = Marshal.ReadInt16(usnRecordPtr, FNL_OFFSET);
            FileNameOffset = Marshal.ReadInt16(usnRecordPtr, FN_OFFSET);
            FileName = Marshal.PtrToStringUni(new IntPtr(usnRecordPtr.ToInt32() + FileNameOffset), FileNameLength / sizeof(char));
        }
    }
}
