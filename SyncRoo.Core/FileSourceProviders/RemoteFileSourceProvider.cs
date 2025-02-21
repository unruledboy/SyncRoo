using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;

namespace SyncRoo.Core.FileSourceProviders
{
    public class RemoteFileSourceProvider(IHttpClientFactory httpClientFactory, ILogger<IReportProducer> logger) : IFileSourceProvider
    {
        public string Name => SourceProviders.Remote;

        public async IAsyncEnumerable<FileDto> Find(ScanTaskDto scanTask, AppSyncSettings syncSettings)
        {
            if (!scanTask.RootFolder.ValidateSyncProtocol(out var server, out var path))
            {
                yield break;
            }

            logger.LogInformation("Sending request to scan {RemoteServer}...", server);

            var httpClient = httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(syncSettings.ProcessTimeoutInSeconds);

            var scanResponseMessage = await ScanFiles(scanTask, server, path, httpClient);

            if (!scanResponseMessage.IsSuccessStatusCode)
            {
                logger.LogError("Request to scan {RemoteServer} failed: {ErrorMessage}", server, await scanResponseMessage.Content.ReadAsStringAsync());

                yield break;
            }

            var scanResult = await scanResponseMessage.Content.ReadFromJsonAsync<ScanResultDto>();
            var runningTotal = 0L;

            logger.LogInformation("Scan found {TotalFileCount} files for {RootFolder} on {RemoteServer}...", scanResult.FileCount, path, server);

            for (var i = 0; i < scanResult.FileCount / syncSettings.FileBatchSize + 1; i++)
            {
                var getResponseMessage = await GetFiles(scanTask, syncSettings, server, path, httpClient, i, logger);

                if (!getResponseMessage.IsSuccessStatusCode)
                {
                    logger.LogError("Request to get files from {RemoteServer} failed: {ErrorMessage}", server, await getResponseMessage.Content.ReadAsStringAsync());

                    yield break;
                }

                var files = await getResponseMessage.Content.ReadFromJsonAsync<List<FileDto>>();

                runningTotal += files.Count;

                logger.LogInformation("Found {FileCount} ({RunningTotal}/{TotalFileCount}) files for {RootFolder} on {RemoteServer}...", files.Count, runningTotal, scanResult.FileCount, path, server);

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

            var teardownResponseMessage = await Teardown(logger, scanTask, server, path, httpClient);

            if (!teardownResponseMessage.IsSuccessStatusCode)
            {
                logger.LogError("Request to teardown transient info {RemoteServer} failed: {ErrorMessage}", server, await teardownResponseMessage.Content.ReadAsStringAsync());

                yield break;
            }

            logger.LogInformation("Transient info for {RootFolder} on {RemoteServer} torn down.", path, server);
        }

        private static async Task<HttpResponseMessage> Teardown(ILogger logger, ScanTaskDto scanTask, string server, string path, HttpClient httpClient)
        {
            logger.LogInformation("Sending request to teardown transient info for {RootFolder} on {RemoteServer}...", path, server);

            var teardownUrl = GetApiUrl(server, "teardown");
            var teardownPayload = JsonSerializer.Serialize(new TeardownRequestDto
            {
                Folder = scanTask.RootFolder
            });
            var teardownContent = new StringContent(teardownPayload, Encoding.UTF8, MediaTypeNames.Application.Json);
            var teardownResponseMessage = await httpClient.PostAsync(teardownUrl, teardownContent);

            return teardownResponseMessage;
        }

        private static string GetApiUrl(string server, string operation)
            => $"http://{server}/{operation}";

        private static async Task<HttpResponseMessage> GetFiles(ScanTaskDto scanTask, AppSyncSettings syncSettings, string server, string path, HttpClient httpClient, int page, ILogger logger)
        {
            logger.LogInformation("Sending request to get files from {RootFolder} on {RemoteServer}...", path, server);

            var getUrl = GetApiUrl(server, "get");
            var getPayload = JsonSerializer.Serialize(new GetFileRequestDto
            {
                FileMode = scanTask.FileMode,
                Page = page,
                Size = syncSettings.FileBatchSize
            });
            var getContent = new StringContent(getPayload, Encoding.UTF8, MediaTypeNames.Application.Json);
            var getResponseMessage = await httpClient.PostAsync(getUrl, getContent);

            return getResponseMessage;
        }

        private static async Task<HttpResponseMessage> ScanFiles(ScanTaskDto scanTask, string server, string path, HttpClient httpClient)
        {
            var scanUrl = GetApiUrl(server, "scan");
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

            return scanResponseMessage;
        }

        public void Init()
        {
        }

        public bool IsSupported(string folder, bool usnJournal)
            => folder.ValidateSyncProtocol(out _, out _);
    }
}
