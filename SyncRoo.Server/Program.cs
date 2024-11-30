using Serilog;
using SyncRoo.Core.FileSourceProviders;
using SyncRoo.Core.FileStorageProviders;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.Models.Dtos;
using SyncRoo.Core.ReportProducers;
using SyncRoo.Core.Services;
using SyncRoo.Core.Utils;
using SyncRoo.Server.Handlers;

namespace SyncRoo.Server
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("Starting up!");

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.release.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<IConfiguration>(configuration);
            builder.Services.AddSingleton<IScanService, FileSystemScanService>();

            builder.Services.AddSingleton<IFileSourceProvider, NativeFileSourceProvider>();
            builder.Services.AddSingleton<IFileSourceProvider, NtfsUsnJournalFileSourceProvider>();

            builder.Services.AddSingleton<IReportProducer, LogReportProducer>();
            builder.Services.AddSingleton<IReportProducer, FileReportProducer>();

            switch (configuration["Sync:FilStorageProvider"]?.ToLowerInvariant())
            {
                case StorageProviders.InMemory:
                    builder.Services.AddSingleton<IFileStorageProvider, InMemoryFileStorageProvider>();
                    break;
                case StorageProviders.SqlServer:
                case StorageProviders.SqlServerLocalDB:
                    builder.Services.AddSingleton<IFileStorageProvider, SqlServerFileStorageProvider>();
                    break;
                default:
                    builder.Services.AddSingleton<IFileStorageProvider, SqliteFileStorageProvider>();
                    break;
            }

            builder.Services.AddSingleton<ScanHandler>();

            builder.Services.Configure<AppSyncSettings>(configuration.GetSection("Sync"));
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));
            builder.Logging.AddSerilog(Log.Logger);

            var app = builder.Build();

            app.MapGet("/", () => "Hello SyncRoo!");

            app.MapPost("/scan", async (ScanHandler scanService, ScanTaskDto scanTask) => 
            {
                return Results.Ok(await scanService.Run(scanTask));
            });

            app.MapPost("/get", async (ScanHandler scanService, GetFileRequestDto getFileRequest) =>
            {
                return Results.Ok(await scanService.GetFiles(getFileRequest));
            });

            app.MapPost("/teardown", async (ScanHandler scanService, TeardownRequestDto teardownRequest) =>
            {
                await scanService.Teardown(teardownRequest);

                return Results.Ok();
            });

            app.Run();
        }
    }
}
