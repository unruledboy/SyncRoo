using System.Data;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SyncRoo.Core;
using SyncRoo.FileStorageProviders;
using SyncRoo.Interfaces;
using SyncRoo.Models;
using SyncRoo.Utils;

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

                    switch (configuration["Sync:FilStorageProvider"]?.ToLowerInvariant())
                    {
                        case StorageProviders.Sqlite:
                            services.AddSingleton<IFileStorageProvider, SqliteFileStorageProvider>();
                            break;
                        default:
                            services.AddSingleton<IFileStorageProvider, SqlServerFileStorageProvider>();
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

                            if (!ValidateOptions(opts, configuration))
                            {
                                return;
                            }

                            var logger = host.Services.GetService<ILogger<Engine>>();
                            var syncSettings = host.Services.GetService<IOptions<AppSyncSettings>>();
                            var fileStorageProvider = host.Services.GetService<IFileStorageProvider>();
                            var engine = new Engine(opts, syncSettings, fileStorageProvider, logger);

                            await engine.Sync();
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

        private static bool ValidateOptions(CommandOptions opts, IConfiguration configuration)
        {
            const string ConnectionStringDatabase = "Database";
            var databaseConnectionString = configuration.GetConnectionString(ConnectionStringDatabase);

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
