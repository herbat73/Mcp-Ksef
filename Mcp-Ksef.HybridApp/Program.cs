using Shared.Configurations;
using Shared.Extensions;
using McpKsef.HybridApp.Helpers;

if (!RunInfoHelper.IsSettingsValidToRun()) return;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);
var builder = AppBuilderHelper.Setup(useStreamableHttp, args);

var app = builder.BuildApp(useStreamableHttp);

using (var scope = app.Services.CreateScope())
{
}

if (useStreamableHttp)
{
    (app as WebApplication)!.MapOpenApi("/{documentName}.json");
}



await app.RunAsync();


