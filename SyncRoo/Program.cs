﻿using System.Data;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SyncRoo.Core;
using SyncRoo.Core.FileSourceProviders;
using SyncRoo.Core.FileStorageProviders;
using SyncRoo.Core.Interfaces;
using SyncRoo.Core.Models;
using SyncRoo.Core.ReportProducers;
using SyncRoo.Core.Services;
using SyncRoo.Core.Utils;

namespace SyncRoo
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .CreateLogger();

            Log.Information("Starting up!");

            var parserResult = Parser.Default.ParseArguments<CommandOptions>(args);

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.release.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.AddSerilog(Log.Logger);
                })
                .ConfigureServices((x, services) =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddSingleton<IScanService, FileSystemScanService>();
                    services.AddHttpClient();

                    services.AddSingleton<IFileSourceProvider, RemoteFileSourceProvider>();
                    services.AddSingleton<IFileSourceProvider, NativeFileSourceProvider>();
                    services.AddSingleton<IFileSourceProvider, NtfsUsnJournalFileSourceProvider>();

                    services.AddSingleton<IReportProducer, LogReportProducer>();
                    services.AddSingleton<IReportProducer, FileReportProducer>();

                    switch (configuration["Sync:FilStorageProvider"]?.ToLowerInvariant())
                    {
                        case StorageProviders.InMemory:
                            services.AddSingleton<IFileStorageProvider, InMemoryFileStorageProvider>();
                            break;
                        case StorageProviders.SqlServer:
                        case StorageProviders.SqlServerLocalDB:
                            services.AddSingleton<IFileStorageProvider, SqlServerFileStorageProvider>();
                            break;
                        default:
                            services.AddSingleton<IFileStorageProvider, SqliteFileStorageProvider>();
                            break;
                    }

                    services.Configure<AppSyncSettings>(configuration.GetSection("Sync"));
                    services.AddLogging(builder => builder.AddSerilog(dispose: true));
                })
                .UseSerilog((context, configuration) =>
                {
                    configuration.ReadFrom.Configuration(context.Configuration);
                });

            using IHost host = builder.Build();

            try
            {
                await parserResult.MapResult(
                        async opts =>
                        {
                            var configuration = host.Services.GetService<IConfiguration>();
                            var logger = host.Services.GetService<ILogger<IReportProducer>>();

                            if (!ValidateOptions(opts, configuration, logger))
                            {
                                return;
                            }

                            var syncSettings = host.Services.GetService<IOptions<AppSyncSettings>>();
                            var fileStorageProvider = host.Services.GetService<IFileStorageProvider>();
                            var fileSourceProviders = host.Services.GetService<IEnumerable<IFileSourceProvider>>();
                            var reportProducers = host.Services.GetService<IEnumerable<IReportProducer>>();
                            var scanService = host.Services.GetService<IScanService>();
                            var syncEngine = new SyncEngine(opts, syncSettings, fileStorageProvider, fileSourceProviders, reportProducers, scanService, logger);

                            await syncEngine.Sync();
                        },
                async errors => await HandleParserError(errors)
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Run was interrupted.");
            }

            Log.Information("Run finished");
        }

        private static bool ValidateOptions(CommandOptions opts, IConfiguration configuration, ILogger<IReportProducer> logger)
        {
            var databaseConnectionString = configuration.GetConnectionString(ConnectionStrings.Database);

            if (string.IsNullOrWhiteSpace(opts.SourceFolder) && string.IsNullOrWhiteSpace(opts.TargetFolder) && string.IsNullOrWhiteSpace(opts.Profile))
            {
                logger.LogError("Please either provide the source/target folders, or the profile file.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(opts.DatabaseConnectionString))
            {
                databaseConnectionString = opts.DatabaseConnectionString;
            }

            if (string.IsNullOrWhiteSpace(databaseConnectionString))
            {
                Console.WriteLine("Please provide the connection string of the SOURCE database:");
                databaseConnectionString = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(databaseConnectionString))
            {
                Console.WriteLine("Please provide SORUCE db connection string");

                return false;
            }

            opts.DatabaseConnectionString = databaseConnectionString;

            return true;
        }

        private static async Task<int> HandleParserError(IEnumerable<Error> errors)
        {
            var errorText = string.Join(Environment.NewLine, errors.Select(x => x.ToString()));
            Log.Error("Error in parsing command line: {ErrorText}", errorText);

            return await Task.FromResult(-1);
        }
    }
}
