using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.FileStorageProviders
{
    public class SqliteFileStorageProvider : IFileStorageProvider
    {
        public async Task<long> GetPendingFileCount(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);

            var pendingFileCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM PendingFile");

            return pendingFileCount;
        }

        public async Task<List<PendingFileDto>> GetPendingFiles(string connectionString, long lastId, int batchSize)
        {
            using var connection = new SqliteConnection(connectionString);

            var result = (await connection.QueryAsync<SqlitePendingFileDto>("SELECT * FROM PendingFile WHERE Id > @LastId ORDER BY Id LIMIT @Next OFFSET 0",
                new { LastId = lastId, Next = batchSize })).ToList();

            return result.Select(x => new PendingFileDto
            {
                Id = x.Id,
                FileName = x.FileName,
                Size = x.Size,
                ModifiedTime = DateTime.UnixEpoch.AddSeconds(x.ModifiedTime)
            }).ToList();
        }

        public async Task<List<FileDto>> GetSourceFiles(string connectionString, long lastId, int batchSize, ILogger logger)
        {
            logger.LogInformation("Getting files in source...");

            using var connection = new SqliteConnection(connectionString);

            var result = (await connection.QueryAsync<SqliteFileDto>("DELETE FROM SourceFile WHERE FileName IN (SELECT FileName FROM SourceFile ORDER BY FileName LIMIT @Next OFFSET 0) RETURNING *",
                new { Next = batchSize })).ToList();

            logger.LogInformation("Found {FileCount} in source", result.Count);

            return result.Select(x => new FileDto
            {
                FileName = x.FileName,
                Size = x.Size,
                ModifiedTime = DateTime.UnixEpoch.AddSeconds(x.ModifiedTime)
            }).ToList();
        }

        public async Task<List<FileDto>> GetTargetFiles(string connectionString, long lastId, int batchSize, ILogger logger)
        {
            using var connection = new SqliteConnection(connectionString);

            var result = (await connection.QueryAsync<SqliteFileDto>("DELETE FROM TargetFile WHERE FileName IN (SELECT FileName FROM TargetFile ORDER BY FileName LIMIT @Next OFFSET 0) RETURNING *",
                new { Next = batchSize })).ToList();

            logger.LogInformation("Found {FileCount} in target", result.Count);

            return result.Select(x => new FileDto
            {
                FileName = x.FileName,
                Size = x.Size,
                ModifiedTime = DateTime.UnixEpoch.AddSeconds(x.ModifiedTime)
            }).ToList();
        }

        public virtual async Task Initialize(string connectionString, ILogger logger)
        {
            logger.LogInformation("Initializing for provider {FileStorageProvider}...", nameof(SqliteFileStorageProvider));

            using var connection = new SqliteConnection(connectionString);
            var sqlText = FileSystemStorage.GetProviderContent($"{nameof(SqliteFileStorageProvider)}.sql");

            await connection.ExecuteAsync(sqlText);

            logger.LogInformation("Initialized for provider {FileStorageProvider}.", nameof(SqliteFileStorageProvider));
        }

        public async Task PrepareFileStorage(string connectionString, SyncFileMode fileMode, ILogger logger)
        {
            var tableName = fileMode switch
            {
                SyncFileMode.Source => "SourceFile",
                SyncFileMode.Target => "TargetFile",
                _ => "PendingFile"
            };
            using var connection = new SqliteConnection(connectionString);

            await connection.ExecuteAsync($"DELETE FROM {tableName}");
        }

        private static async Task ClearnupFileStorage(string connectionString, SyncFileMode fileMode)
        {
            var tableName = fileMode switch
            {
                SyncFileMode.Source => "SourceFile",
                SyncFileMode.Target => "TargetFile",
                _ => "PendingFile"
            };
            using var connection = new SqliteConnection(connectionString);

            await connection.ExecuteAsync($"DROP TABLE IF EXISTS {tableName}");
        }

        public async Task Run(AppSyncSettings syncSettings, string connectionString, SyncTaskDto task, ILogger logger)
        {
            await PrepareFileStorage(connectionString, SyncFileMode.Pending, logger);

            using var connection = new SqliteConnection(connectionString);

            const string SqlText = @"pragma journal_mode=OFF;
    INSERT INTO PendingFile (FileName, Size, ModifiedTime)
		SELECT sf.FileName, sf.Size, sf.ModifiedTime FROM SourceFile sf
			LEFT OUTER JOIN TargetFile tf ON sf.FileName = tf.FileName
			WHERE tf.FileName IS NULL
            OR (@Rule = 'standard' AND (sf.Size <> tf.Size OR sf.ModifiedTime <> tf.ModifiedTime))
			OR (@Rule = 'newer' AND sf.ModifiedTime > tf.ModifiedTime)
			OR (@Rule = 'larger' AND sf.Size > tf.Size);";

            await connection.ExecuteAsync(SqlText, new { task.Rule }, commandTimeout: syncSettings.CommandTimeoutInSeconds);
        }

        public async Task Save(AppSyncSettings syncSettings, string connectionString, long runtimeTotal, List<FileDto> files, SyncFileMode fileMode, ILogger logger)
        {
            if (fileMode != SyncFileMode.Source && fileMode != SyncFileMode.Target)
            {
                throw new ArgumentOutOfRangeException(nameof(fileMode));
            }

            const int BatchValueSize = 1000;
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            using var command = connection.CreateCommand();

            var tableName = fileMode switch
            {
                SyncFileMode.Source => "SourceFile",
                _ => "TargetFile",
            };

            foreach (var chunkedFiles in files.Chunk(BatchValueSize))
            {
                // Concatenating multiple values in one query is faster than individual calls.
                var valueList = string.Join(',', chunkedFiles.Select(x => $"('{x.FileName}', {x.Size}, {Convert.ToInt64((x.ModifiedTime - DateTime.UnixEpoch).TotalSeconds)})"));
                command.CommandText = @$"pragma journal_mode=OFF;
INSERT INTO {tableName} (FileName, Size, ModifiedTime) VALUES {valueList}";
                command.CommandTimeout = syncSettings.CommandTimeoutInSeconds;

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            logger.LogInformation("Saved metadata of {FileCount} files, totally {TotalFileCount} files found.", files.Count, runtimeTotal);

            // This is to avoid hogging the CPU
            await Task.Delay(syncSettings.OperationDelayInMs);
        }

        public virtual async Task Teardown(string connectionString, ILogger logger)
        {
            await ClearnupFileStorage(connectionString, SyncFileMode.Source);
            await ClearnupFileStorage(connectionString, SyncFileMode.Target);
            await ClearnupFileStorage(connectionString, SyncFileMode.Pending);

            using var connection = new SqliteConnection(connectionString);

            await connection.ExecuteAsync("VACUUM");
        }
    }
}
