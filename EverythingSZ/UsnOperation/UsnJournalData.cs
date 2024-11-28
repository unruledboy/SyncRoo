using System.IO;
using EverythingSZ.PInvoke.Structures;

namespace EverythingSZ.UsnOperation
{
    public class UsnJournalData
    {
        public DriveInfo Drive { get; private set; }
        public ulong UsnJournalID { get; private set; }
        public long FirstUsn { get; private set; }
        public long NextUsn { get; private set; }
        public long LowestValidUsn { get; private set; }
        public long MaxUsn { get; private set; }
        public ulong MaximumSize { get; private set; }
        public ulong AllocationDelta { get; private set; }

        public UsnJournalData(DriveInfo drive, USN_JOURNAL_DATA ntfsUsnJournalData)
        {
            Drive = drive;
            UsnJournalID = ntfsUsnJournalData.UsnJournalID;
            FirstUsn = ntfsUsnJournalData.FirstUsn;
            NextUsn = ntfsUsnJournalData.NextUsn;
            LowestValidUsn = ntfsUsnJournalData.LowestValidUsn;
            MaxUsn = ntfsUsnJournalData.MaxUsn;
            MaximumSize = ntfsUsnJournalData.MaximumSize;
            AllocationDelta = ntfsUsnJournalData.AllocationDelta;
        }

        // pesudo-code for checking valid USN journal
        //private bool IsUsnJournalValid()
        //{

        //    bool isValid = true;
        //    //
        //    // is the JournalID from the previous state == JournalID from current state?
        //    //
        //    if (_previousUsnState.UsnJournalID == _currentUsnState.UsnJournalID)
        //    {
        //        //
        //        // is the next usn to process still available
        //        //
        //        if (_previousUsnState.NextUsn > _currentUsnState.FirstUsn && _previousUsnState.NextUsn < _currentUsnState.NextUsn)
        //        {
        //            isValid = true;
        //        }
        //        else
        //        {
        //            isValid = false;
        //        }
        //    }
        //    else
        //    {
        //        isValid = false;
        //    }

        //    return isValid;
        //}
    }
}
