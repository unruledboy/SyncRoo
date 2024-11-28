using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using EverythingSZ.PInvoke.Structures;
using EverythingSZ.PInvoke.Constants;
using EverythingSZ.PInvoke;

namespace EverythingSZ.UsnOperation
{
    public class UsnOperator : IDisposable
    {
        protected USN_JOURNAL_DATA ntfsUsnJournalData;

        public DriveInfo Drive
        {
            get;
            private set;
        }

        public string DriveLetter
        {
            get
            {
                return Drive.Name.TrimEnd(new char[] { '\\', ':' });
            }
        }

        public IntPtr DriveRootHandle
        {
            get;
            private set;
        }

        public UsnOperator(DriveInfo drive)
        {
            if (string.Compare(drive.DriveFormat, "ntfs", true) != 0)
            {
                throw new ArgumentException("USN journal only exists in NTFS drive.");
            }

            Drive = drive;
            DriveRootHandle = GetRootHandle();
            ntfsUsnJournalData = new USN_JOURNAL_DATA();
        }

        public UsnJournalData GetUsnJournal()
        {
            UsnErrorCode usnErrorCode = QueryUSNJournal();

            UsnJournalData result = null;

            if (usnErrorCode == UsnErrorCode.SUCCESS)
            {
                result = new UsnJournalData(Drive, ntfsUsnJournalData);
            }

            return result;
        }

        public bool CreateUsnJournal(ulong maximumSize = 0x10000000, ulong allocationDelta = 0x100000)
        {
            uint bytesReturnedCount;

            var createUsnJournalData = new CREATE_USN_JOURNAL_DATA();
            createUsnJournalData.MaximumSize = maximumSize;
            createUsnJournalData.AllocationDelta = allocationDelta;

            int sizeCujd = Marshal.SizeOf(createUsnJournalData);

            IntPtr cujdBuffer = GetHeapGlobalPtr(sizeCujd);

            Marshal.StructureToPtr(createUsnJournalData, cujdBuffer, true);

            bool isSuccess = Win32Api.DeviceIoControl(
                DriveRootHandle,
                UsnControlCode.FSCTL_CREATE_USN_JOURNAL,
                cujdBuffer,
                sizeCujd,
                IntPtr.Zero,
                0,
                out bytesReturnedCount,
                IntPtr.Zero);

            Marshal.FreeHGlobal(cujdBuffer);

            return isSuccess;
        }

        public List<UsnEntry> GetEntries()
        {
            var result = new List<UsnEntry>();

            UsnErrorCode usnErrorCode = QueryUSNJournal();

            if (usnErrorCode == UsnErrorCode.SUCCESS)
            {
                MFT_ENUM_DATA mftEnumData = new MFT_ENUM_DATA();
                mftEnumData.StartFileReferenceNumber = 0;
                mftEnumData.LowUsn = 0;
                mftEnumData.HighUsn = ntfsUsnJournalData.NextUsn;

                int sizeMftEnumData = Marshal.SizeOf(mftEnumData);

                IntPtr ptrMftEnumData = GetHeapGlobalPtr(sizeMftEnumData);

                Marshal.StructureToPtr(mftEnumData, ptrMftEnumData, true);

                int ptrDataSize = sizeof(ulong) + 10000;

                IntPtr ptrData = GetHeapGlobalPtr(ptrDataSize);

                uint outBytesCount;

                while (false != Win32Api.DeviceIoControl(
                    DriveRootHandle,
                    UsnControlCode.FSCTL_ENUM_USN_DATA,
                    ptrMftEnumData,
                    sizeMftEnumData,
                    ptrData,
                    ptrDataSize,
                    out outBytesCount,
                    IntPtr.Zero))
                {
                    // ptrData includes following struct:
                    //typedef struct
                    //{
                    //    USN             LastFileReferenceNumber;
                    //    USN_RECORD_V2   Record[1];
                    //} *PENUM_USN_DATA;

                    IntPtr ptrUsnRecord = new IntPtr(ptrData.ToInt32() + sizeof(long));

                    while (outBytesCount > 60)
                    {
                        var usnRecord = new USN_RECORD_V2(ptrUsnRecord);

                        result.Add(new UsnEntry(usnRecord));

                        ptrUsnRecord = new IntPtr(ptrUsnRecord.ToInt32() + usnRecord.RecordLength);

                        outBytesCount -= usnRecord.RecordLength;
                    }

                    Marshal.WriteInt64(ptrMftEnumData, Marshal.ReadInt64(ptrData, 0));
                }

                Marshal.FreeHGlobal(ptrData);
                Marshal.FreeHGlobal(ptrMftEnumData);
            }

            return result;
        }

        private static IntPtr GetHeapGlobalPtr(int size)
        {
            IntPtr buffer = Marshal.AllocHGlobal(size);
            Win32Api.ZeroMemory(buffer, size);

            return buffer;
        }

        private UsnErrorCode QueryUSNJournal()
        {
            int sizeUsnJournalData = Marshal.SizeOf(ntfsUsnJournalData);

            USN_JOURNAL_DATA tempUsnJournalData;

            uint bytesReturnedCount;

            bool isSuccess = Win32Api.DeviceIoControl(
                DriveRootHandle,
                UsnControlCode.FSCTL_QUERY_USN_JOURNAL,
                IntPtr.Zero,
                0,
                out tempUsnJournalData,
                sizeUsnJournalData,
                out bytesReturnedCount,
                IntPtr.Zero);

            ntfsUsnJournalData = tempUsnJournalData;

            //if (isSuccess)
            //{
            //    return tempUsnJournalData;
            //}
            //else
            //{
            //int win32ErrorCode = Marshal.GetLastWin32Error();
            //if (Enum.IsDefined(typeof(UsnErrorCode), win32ErrorCode))
            //{
            //    var usnErrorCode = (UsnErrorCode)win32ErrorCode;
            //}

            //    throw new IOException("Drive returned false for Query Usn Journal", new Win32Exception(win32ErrorCode));
            //}

            return (UsnErrorCode)Marshal.GetLastWin32Error();
        }

        private IntPtr GetRootHandle()
        {
            string volume = string.Format("\\\\.\\{0}:", DriveLetter);

            var result = Win32Api.CreateFile(
                volume,
                Win32ApiConstant.GENERIC_READ,
                Win32ApiConstant.FILE_SHARE_READ | Win32ApiConstant.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Win32ApiConstant.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (result.ToInt32() == Win32ApiConstant.INVALID_HANDLE_VALUE)
            {
                throw new IOException("Drive returned invalid root handle", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return result;
        }

        public void Dispose()
        {
            if (DriveRootHandle != null && DriveRootHandle.ToInt32() != Win32ApiConstant.INVALID_HANDLE_VALUE)
            {
                Win32Api.CloseHandle(DriveRootHandle);
            }
        }
    }
}
