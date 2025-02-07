﻿namespace SyncRoo.Core.Interfaces
{
    public interface IReportProducer
    {
        Task Write(DateTime startedTime, string reportType, string batchFolder, List<string> items);
    }
}
