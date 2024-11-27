using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SyncRoo.Interfaces;
using SyncRoo.Models;
using SyncRoo.Utils;

namespace SyncRoo.FileStorageProviders
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

            var result = (await connection.QueryAsync<PendingFileDto>("SELECT * FROM PendingFile WHERE Id > @LastId ORDER BY Id LIMIT @Next OFFSET 0",
                new { lastId, Next = batchSize })).ToList();

            return result;
        }

        public virtual async Task Initialize(string connectionString, ILogger logger)
        {
            logger.LogInformation("Initializing for provider {FileStorageProvider}", nameof(SqliteFileStorageProvider));

            using var connection = new SqliteConnection(connectionString);
            var sqlText = FileSystemStorage.GetProviderContent($"{nameof(SqliteFileStorageProvider)}.sql");

            await connection.ExecuteAsync(sqlText);

            logger.LogInformation("Initialized for provider {FileStorageProvider}", nameof(SqliteFileStorageProvider));
        }

        public async Task PrepareFolder(string connectionString, SyncFileMode fileMode, ILogger logger)
            => await ResetFolder(connectionString, fileMode);

        private static async Task ResetFolder(string connectionString, SyncFileMode fileMode)
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

        public async Task Run(AppSyncSettings syncSettings, string connectionString, ILogger logger)
        {
            await ResetFolder(connectionString, SyncFileMode.Pending);

            using var connection = new SqliteConnection(connectionString);

            const string SqlText = @"INSERT INTO PendingFile (FileName, Size, ModifiedTime)
		SELECT sf.FileName, sf.Size, sf.ModifiedTime FROM SourceFile sf
			LEFT OUTER JOIN TargetFile tf ON sf.FileName = tf.FileName
			WHERE tf.FileName IS NULL OR sf.Size <> tf.Size OR sf.ModifiedTime > tf.ModifiedTime";

            await connection.ExecuteAsync(SqlText, commandTimeout: syncSettings.CommandTimeoutInSeconds);
        }

        public async Task Save(AppSyncSettings syncSettings, string connectionString, List<FileDto> files, SyncFileMode fileMode, ILogger logger)
        {
            if (fileMode != SyncFileMode.Source && fileMode != SyncFileMode.Target)
            {
                throw new ArgumentOutOfRangeException(nameof(fileMode));
            }

            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();
            using var command = connection.CreateCommand();

            var tableName = fileMode switch
            {
                SyncFileMode.Source => "SourceFile",
                _ => "TargetFile",
            };
            command.CommandText = $"INSERT INTO {tableName} (FileName, Size, ModifiedTime) VALUES (@FileName, @Size, @ModifiedTime)";
            command.CommandTimeout = syncSettings.CommandTimeoutInSeconds;

            var parameterNames = new[]
            {
                "@FileName",
                "@Size",
                "@ModifiedTime"
            };

            var parameters = parameterNames.Select(parameterName =>
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = parameterName;
                command.Parameters.Add(parameter);

                return parameter;
            }).ToArray();

            await command.PrepareAsync();

            foreach (var fileDto in files)
            {
                parameters[0].Value = fileDto.FileName;
                parameters[1].Value = fileDto.Size;
                parameters[2].Value = fileDto.ModifiedTime;

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            logger.LogInformation("Saved {FileCount} files", files.Count);

            // This is to avoid hogging the CPU
            await Task.Delay(syncSettings.OperationDelayInMs);
        }

        public virtual async Task Teardown(string connectionString, ILogger logger)
        {
            await ResetFolder(connectionString, SyncFileMode.Source);
            await ResetFolder(connectionString, SyncFileMode.Target);
            await ResetFolder(connectionString, SyncFileMode.Pending);
        }
    }
}
