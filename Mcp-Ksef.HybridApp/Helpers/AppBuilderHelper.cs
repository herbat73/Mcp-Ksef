using McpKsef.HybridApp.Configurations;
using Shared.Extensions;
using Shared.OpenApi;

namespace McpKsef.HybridApp.Helpers;

public static class AppBuilderHelper
{
    public static IHostApplicationBuilder Setup(bool useStreamableHttp, string[] args)
    {
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
        
        builder.Configuration.GetSection("Connection").Bind(builder.Configuration);
        
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