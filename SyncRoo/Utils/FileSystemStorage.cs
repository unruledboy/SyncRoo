namespace SyncRoo.Utils
{
    public static class FileSystemStorage
    {
        private const string FolderFileStorage = "FileStorage";

        public static string GetProviderContent(string file)
        {
            var fullFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderFileStorage, file);
            var content = File.ReadAllText(fullFileName);

            return content;
        }
    }
}
