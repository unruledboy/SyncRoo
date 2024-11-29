﻿using SyncRoo.Utils;

namespace SyncRoo.Models.Dtos
{
    public class ScanTaskDto
    {
        public string RootFolder { get; set; }
        public List<string> FilePatterns { get; set; }
        public string Rule { get; set; }
        public SyncFileMode FileMode { get; set; }
    }
}
