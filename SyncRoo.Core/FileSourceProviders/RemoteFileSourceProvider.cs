using System.Text;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.Utils;
using System.Text.Json;
using System.Net.Mime;
using System.Net.Http.Json;
using SyncRoo.Core.Models;

namespace SyncRoo.Core.FileSourceProviders
{
    public class RemoteFileSourceProvider(IHttpClientFactory httpClientFactory) : IFileSourceProvider
    {
        public string Name => SourceProviders.Native;

        public async IAsyncEnumerable<FileDto> Find(ScanTaskDto scanTask, AppSyncSettings syncSettings)
        {
            var httpClient = httpClientFactory.CreateClient();

            scanTask.RootFolder.ValidateNetworkFolder(out var server, out var path);

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

            if (scanResponseMessage.IsSuccessStatusCode)
            {
                var scanResult = await scanResponseMessage.Content.ReadFromJsonAsync<ScanResultDto>();

                for (var i = 0; i < scanResult.FileCount / syncSettings.FileBatchSize + 1; i++)
                {
                    var getUrl = $"{server}/scan";
                    var getPayload = JsonSerializer.Serialize(new GetFileRequestDto
                    {
                        Page = i,
                        Size = syncSettings.FileBatchSize
                    });
                    var getContent = new StringContent(getPayload, Encoding.UTF8, MediaTypeNames.Application.Json);
                    var getResponseMessage = await httpClient.PostAsync(getUrl, getContent);

                    if (getResponseMessage.IsSuccessStatusCode)
                    {
                        var files = await getResponseMessage.Content.ReadFromJsonAsync<List<FileDto>>();

                        if (files?.Count > 0)
                        {
                            foreach (var file in files)
                            {
                                yield return file;
                            }
                        }
                    }
                }
            }
        }

        public void Init()
        {
        }

        public bool IsSupported(string folder, bool usnJournal)
            => folder.ValidateNetworkFolder(out _, out _);
    }
}
