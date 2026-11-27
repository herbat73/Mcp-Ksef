using McpKsef.HybridApp.Configurations;
using Shared.Extensions;
using Shared.OpenApi;

namespace McpKsef.HybridApp.Helpers;

public static class AppBuilderHelper
{
    public static IHostApplicationBuilder Setup(bool useStreamableHttp, string[] args)
    {
        // The host parses arguments as .NET configuration, where a valueless app flag (--http) would swallow
        // the next argument (e.g. --environment), so it gets the arguments without the app's own flags.
        var hostArgs = Shared.Configurations.AppSettings.RemoveAppFlags(args);
        IHostApplicationBuilder builder = useStreamableHttp
            ? WebApplication.CreateBuilder(hostArgs)
            : Host.CreateApplicationBuilder(hostArgs);

        builder.Services.AddAppSettings<KsefAppSettings>(builder.Configuration, args);

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            consoleLogOptions.TimestampFormat = "[yyyy-MM-dd HH:mm:ss UTC] ";
            consoleLogOptions.UseUtcTimestamp = true;
        });
        
        if (useStreamableHttp)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddOpenApi("swagger", o =>
            {
                o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi2_0;
                o.AddDocumentTransformer<McpDocumentTransformer<KsefAppSettings>>();
            });
            builder.Services.AddOpenApi("openapi", o =>
            {
                o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
                o.AddDocumentTransformer<McpDocumentTransformer<KsefAppSettings>>();
            });
        }
        
        return builder;
    }
}