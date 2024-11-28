// -----------------------------------------------------------------------
// <copyright file="UsnEntry.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

using EverythingSZ.PInvoke.Constants;
using EverythingSZ.PInvoke.Structures;

namespace EverythingSZ.UsnOperation
{
    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class UsnEntry
    {
        public uint RecordLength { get; private set; }
        public ulong FileReferenceNumber { get; private set; }

        /// <summary>
        /// Gets the parent file reference number.
        /// When its values is 1407374883553285(0x5000000000005L), it means this file/folder is under drive root
        /// </summary>
        /// <value>
        /// The parent file reference number.
        /// </value>
        public ulong ParentFileReferenceNumber { get; private set; }
        public long Usn { get; private set; }
        public uint Reason { get; private set; }
        public uint FileAttributes { get; private set; }
        public int FileNameLength { get; private set; }
        public int FileNameOffset { get; private set; }
        public string FileName { get; private set; }
        public bool IsFolder
        {
            get
            {
                return (FileAttributes & Win32ApiConstant.FILE_ATTRIBUTE_DIRECTORY) != 0;
            }
        }

        public UsnEntry(USN_RECORD_V2 usnRecord)
        {
            RecordLength = usnRecord.RecordLength;
            FileReferenceNumber = usnRecord.FileReferenceNumber;
            ParentFileReferenceNumber = usnRecord.ParentFileReferenceNumber;
            Usn = usnRecord.Usn;
            Reason = usnRecord.Reason;
            FileAttributes = usnRecord.FileAttributes;
            FileNameLength = usnRecord.FileNameLength;
            FileNameOffset = usnRecord.FileNameOffset;
            FileName = usnRecord.FileName;
        }
    }
}
