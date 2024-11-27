﻿using System.Data;
using Dapper;
using FastMember;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SyncRoo.Interfaces;
using SyncRoo.Models;
using SyncRoo.Utils;

namespace SyncRoo.FileStorageProviders
{
    public class SqlServerFileStorageProvider : IFileStorageProvider
    {
        public async Task<long> GetPendingFileCount(string connectionString)
        {
            using var connection = new SqlConnection(connectionString);

            var pendingFileCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM dbo.PendingFile");

            return pendingFileCount;
        }

        public async Task<List<PendingFileDto>> GetPendingFiles(string connectionString, long lastId, int batchSize)
        {
            using var connection = new SqlConnection(connectionString);

            var result = (await connection.QueryAsync<PendingFileDto>("SELECT * FROM dbo.PendingFile WHERE Id > @LastId ORDER BY Id OFFSET 0 ROWS FETCH NEXT @Next ROWS ONLY",
                new { lastId, Next = batchSize })).ToList();

            return result;
        }

        public virtual async Task Initialize(string connectionString, ILogger logger)
        {
            logger.LogInformation("Initializing for provider {FileStorageProvider}...", nameof(SqlServerFileStorageProvider));

            using var connection = new SqlConnection(connectionString);
            var sqlText = FileSystemStorage.GetProviderContent($"{nameof(SqlServerFileStorageProvider)}.sql");

            await connection.ExecuteAsync(sqlText);

            logger.LogInformation("Initialized for provider {FileStorageProvider}...", nameof(SqlServerFileStorageProvider));
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
            using var connection = new SqlConnection(connectionString);

            await connection.ExecuteAsync($"TRUNCATE TABLE dbo.{tableName}");
        }

        public async Task Run(AppSyncSettings syncSettings, string connectionString, ILogger logger)
        {
            await ResetFolder(connectionString, SyncFileMode.Pending);

            using var connection = new SqlConnection(connectionString);

            await connection.ExecuteAsync("EXEC dbo.usp_AddPendingFiles", commandTimeout: syncSettings.CommandTimeoutInSeconds);
        }

        public async Task Save(AppSyncSettings syncSettings, string connectionString, List<FileDto> files, SyncFileMode fileMode, ILogger logger)
        {
            if (fileMode != SyncFileMode.Source && fileMode != SyncFileMode.Target)
            {
                throw new ArgumentOutOfRangeException(nameof(fileMode));
            }

            const string FileType = "dbo.FileType";
            using var connection = new SqlConnection(connectionString);
            var fileParameters = new DataTable();

            using var parameterReader = ObjectReader.Create(files,
                nameof(FileDto.FileName),
                nameof(FileDto.Size),
                nameof(FileDto.ModifiedTime));
            fileParameters.Load(parameterReader);

            var sqlParameters = new
            {
                Files = fileParameters.AsTableValuedParameter(FileType)
            };

            var sp = fileMode switch
            {
                SyncFileMode.Source => "usp_AddSourceFiles",
                SyncFileMode.Target => "usp_AddTargetFiles",
                _ => default
            };

            await connection.ExecuteAsync($"EXEC dbo.{sp} @Files", sqlParameters, commandTimeout: syncSettings.CommandTimeoutInSeconds);

            logger.LogInformation("Saved {FileCount} files.", files.Count);

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
