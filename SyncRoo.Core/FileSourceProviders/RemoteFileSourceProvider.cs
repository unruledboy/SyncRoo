using System.Text;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;
using System.Text.Json;
using System.Net.Mime;
using System.Net.Http.Json;
using SyncRoo.Core.Models;
using Microsoft.Extensions.Logging;

namespace SyncRoo.Core.FileSourceProviders
{
    public class RemoteFileSourceProvider(IHttpClientFactory httpClientFactory, ILogger logger) : IFileSourceProvider
    {
        public string Name => SourceProviders.Native;

        public async IAsyncEnumerable<FileDto> Find(ScanTaskDto scanTask, AppSyncSettings syncSettings)
        {
            if (!scanTask.RootFolder.ValidateNetworkFolder(out var server, out var path))
            {
                yield break;
            }

            logger.LogInformation("Sending request to scan {RemoteServer}...", server);

            var httpClient = httpClientFactory.CreateClient();
            var scanUrl = $"{server}/scan";
            var remoteTask = new ScanTaskDto
            {
                RootFolder = path,
                FileMode = scanTask.FileMode,
                FilePatterns = scanTask.FilePatterns,
                Limits = scanTask.Limits,
                Rule = scanTask.Rule
            };
            var scanPayload = JsonSerializer.Serialize(remoteTask);
            var scanContent = new StringContent(scanPayload, Encoding.UTF8, MediaTypeNames.Application.Json);
            var scanResponseMessage = await httpClient.PostAsync(scanUrl, scanContent);

            if (!scanResponseMessage.IsSuccessStatusCode)
            {
                logger.LogError("Request to scan {RemoteServer} failed: {ErrorMessage}", server, await scanResponseMessage.Content.ReadAsStringAsync());

                yield break;
            }

            var scanResult = await scanResponseMessage.Content.ReadFromJsonAsync<ScanResultDto>();

            logger.LogInformation("Scan found {FileCount} files for {RootFolder} on {RemoteServer}...", scanResult.FileCount, path, server);

            for (var i = 0; i < scanResult.FileCount / syncSettings.FileBatchSize + 1; i++)
            {
                logger.LogInformation("Sending request to get files from {RootFolder} on {RemoteServer}...", path, server);

                var getUrl = $"{server}/get";
                var getPayload = JsonSerializer.Serialize(new GetFileRequestDto
                {
                    Page = i,
                    Size = syncSettings.FileBatchSize
                });
                var getContent = new StringContent(getPayload, Encoding.UTF8, MediaTypeNames.Application.Json);
                var getResponseMessage = await httpClient.PostAsync(getUrl, getContent);

                if (!getResponseMessage.IsSuccessStatusCode)
                {
                    logger.LogError("Request to get files from {RemoteServer} failed: {ErrorMessage}", server, await getResponseMessage.Content.ReadAsStringAsync());

                    yield break;
                }

                var files = await getResponseMessage.Content.ReadFromJsonAsync<List<FileDto>>();

                logger.LogInformation("Found {FileCount} files for {RootFolder} on {RemoteServer}...", files.Count, path, server);

                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        yield return file;
                    }
                }
                else
                {
                    break;
                }
            }

            logger.LogInformation("Sending request to teardown transient info for {RootFolder} on {RemoteServer}...", path, server);

            var teardownUrl = $"{server}/teardown";
            var teardownPayload = JsonSerializer.Serialize(new TeardownRequestDto
            {
                Folder = scanTask.RootFolder
            });
            var teardownContent = new StringContent(teardownPayload, Encoding.UTF8, MediaTypeNames.Application.Json);
            var teardownResponseMessage = await httpClient.PostAsync(teardownUrl, teardownContent);

            if (!teardownResponseMessage.IsSuccessStatusCode)
            {
                logger.LogError("Request to teardown transient info {RemoteServer} failed: {ErrorMessage}", server, await teardownResponseMessage.Content.ReadAsStringAsync());

                yield break;
            }

            logger.LogInformation("Transient info for {RootFolder} on {RemoteServer} torn down.", path, server);
        }

        public void Init()
        {
        }

        public bool IsSupported(string folder, bool usnJournal)
            => folder.ValidateNetworkFolder(out _, out _);
    }
}
