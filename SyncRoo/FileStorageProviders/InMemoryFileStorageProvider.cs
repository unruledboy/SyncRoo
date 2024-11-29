using Microsoft.Extensions.Logging;
using SyncRoo.Interfaces;
using SyncRoo.Models;
using SyncRoo.Models.Dtos;
using SyncRoo.Utils;

namespace SyncRoo.FileStorageProviders
{
    public class InMemoryFileStorageProvider : IFileStorageProvider
    {
        private readonly Dictionary<string, FileDto> sourceFiles = [];
        private readonly Dictionary<string, FileDto> targetFiles = [];
        private readonly List<PendingFileDto> pendingFiles = [];

        public async Task<long> GetPendingFileCount(string connectionString)
            => await Task.FromResult(pendingFiles.Count);

        public async Task<List<PendingFileDto>> GetPendingFiles(string connectionString, long lastId, int batchSize)
        {
            var batchFiles = pendingFiles.Skip((int)lastId).Take(batchSize);

            var result = batchFiles.Select((x, i) => new PendingFileDto
            {
                Id = i,
                FileName = x.FileName,
                Size = x.Size,
                ModifiedTime = x.ModifiedTime
            }).ToList();

            return await Task.FromResult(result);
        }

        public virtual async Task Initialize(string connectionString, ILogger logger)
        {
            logger.LogInformation("Initializing for provider {FileStorageProvider}...", nameof(InMemoryFileStorageProvider));

            sourceFiles.Clear();
            targetFiles.Clear();
            pendingFiles.Clear();

            logger.LogInformation("Initialized for provider {FileStorageProvider}.", nameof(InMemoryFileStorageProvider));

            await Task.CompletedTask;
        }

        public async Task PrepareFileStorage(string connectionString, SyncFileMode fileMode, ILogger logger)
        {
            switch (fileMode)
            {
                case SyncFileMode.Source:
                    sourceFiles.Clear();
                    break;
                case SyncFileMode.Target:
                    targetFiles.Clear();
                    break;
                case SyncFileMode.Pending:
                    pendingFiles.Clear();
                    break;
            }

            await Task.CompletedTask;
        }

        private async Task ClearnupFileStorage(string connectionString, SyncFileMode fileMode)
            => await PrepareFileStorage(connectionString, fileMode, default);

        public async Task Run(AppSyncSettings syncSettings, string connectionString, SyncTaskDto task, ILogger logger)
        {
            await PrepareFileStorage(connectionString, SyncFileMode.Pending, logger);

            var fileCount = 0;

            foreach (var sourceFile in sourceFiles)
            {
                fileCount++;

                if (!targetFiles.TryGetValue(sourceFile.Key, out var file)
                    || (task.Rule == Rules.Standard && (sourceFile.Value.Size != file.Size || sourceFile.Value.ModifiedTime != file.ModifiedTime))
                    || (task.Rule == Rules.Newer && sourceFile.Value.ModifiedTime > file.ModifiedTime)
                    || (task.Rule == Rules.Larger && sourceFile.Value.Size > file.Size))
                {
                    var fileDto = sourceFile.Value;

                    pendingFiles.Add(new PendingFileDto
                    {
                        Id = fileCount,
                        FileName = fileDto.FileName,
                        Size = fileDto.Size,
                        ModifiedTime = fileDto.ModifiedTime,
                    });
                }
            }
        }

        public async Task Save(AppSyncSettings syncSettings, string connectionString, List<FileDto> files, SyncFileMode fileMode, ILogger logger)
        {
            if (fileMode != SyncFileMode.Source && fileMode != SyncFileMode.Target)
            {
                throw new ArgumentOutOfRangeException(nameof(fileMode));
            }

            switch (fileMode)
            {
                case SyncFileMode.Source:
                    foreach (var item in files)
                    {
                        sourceFiles.TryAdd(item.FileName.ToLowerInvariant(), item);
                    }
                    break;
                case SyncFileMode.Target:
                    foreach (var item in files)
                    {
                        targetFiles.TryAdd(item.FileName.ToLowerInvariant(), item);
                    }
                    break;
            }

            logger.LogInformation("Saved {FileCount} files.", files.Count);

            await Task.CompletedTask;
        }

        public virtual async Task Teardown(string connectionString, ILogger logger)
        {
            await ClearnupFileStorage(connectionString, SyncFileMode.Source);
            await ClearnupFileStorage(connectionString, SyncFileMode.Target);
            await ClearnupFileStorage(connectionString, SyncFileMode.Pending);
        }
    }
}
