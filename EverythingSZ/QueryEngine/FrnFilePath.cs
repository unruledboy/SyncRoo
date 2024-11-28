namespace EverythingSZ.QueryEngine
{
    internal class FrnFilePath
    {
        private ulong fileReferenceNumber;

        private ulong? parentFileReferenceNumber;

        private string fileName;

        private string path;

        public ulong FileReferenceNumber { get { return fileReferenceNumber; } }

        public ulong? ParentFileReferenceNumber { get { return parentFileReferenceNumber; } }

        public string FileName { get { return fileName; } }

        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
            }
        }

        public FrnFilePath(ulong fileReferenceNumber, ulong? parentFileReferenceNumber, string fileName, string path = null)
        {
            this.fileReferenceNumber = fileReferenceNumber;
            this.parentFileReferenceNumber = parentFileReferenceNumber;
            this.fileName = fileName;
            this.path = path;
        }
    }
}
