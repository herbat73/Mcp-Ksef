using McpKsef.HybridApp.Configurations;
using Shared.Configurations;
using Shared.Extensions;
using Shared.OpenApi;

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

var app = builder.BuildApp(useStreamableHttp);

using (var scope = app.Services.CreateScope())
{
}

if (useStreamableHttp)
{
    (app as WebApplication)!.MapOpenApi("/{documentName}.json");
}

await app.RunAsync();


