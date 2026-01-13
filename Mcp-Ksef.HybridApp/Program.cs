using McpKsef.HybridApp.Configurations;
using Shared.Configurations;
using Shared.Extensions;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

IHostApplicationBuilder builder = useStreamableHttp
                                ? WebApplication.CreateBuilder(args)
                                : Host.CreateApplicationBuilder(args);

builder.Services.AddAppSettings<KsefAppSettings>(builder.Configuration, args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    consoleLogOptions.TimestampFormat = "[yyyy-MM-dd HH:mm:ss UTC] ";
    consoleLogOptions.UseUtcTimestamp = true;
});

var app = builder.BuildApp(useStreamableHttp);

await app.RunAsync();


