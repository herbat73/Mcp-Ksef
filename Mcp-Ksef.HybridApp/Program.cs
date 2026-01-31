using KSeF.Client.DI;
using Shared.Configurations;
using Shared.Extensions;
using McpKsef.HybridApp.Helpers;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);
var builder = AppBuilderHelper.Setup(useStreamableHttp, args);

builder.Services.AddKSeFClient(options =>
{
    var useProductionServer = AppSettings.UseProductionServer(Environment.GetEnvironmentVariables(), args);
    options.BaseUrl = useProductionServer ? KsefEnvironmentsUris.PROD : KsefEnvironmentsUris.TEST;
    Console.WriteLine($"useProductionServer : {useProductionServer} - KSeF API URL: {options.BaseUrl} used");
    
    options.CustomHeaders =
        builder.Configuration
            .GetSection("ApiSettings:customHeaders")
            .Get<Dictionary<string, string>>()
        ?? new Dictionary<string, string>();
    
    options.ResourcesPath = builder.Configuration.GetSection("ApiSettings")
        .GetValue<string>("ResourcesPath") ?? null;
    
    options.DefaultCulture = builder.Configuration.GetSection("ApiSettings")
        .GetValue<string>("DefaultCulture") ?? null;
    
    options.SupportedCultures = builder.Configuration.GetSection("ApiSettings").GetSection("SupportedCultures").Get<string[]>() ?? null;
    options.SupportedUICultures = builder.Configuration.GetSection("ApiSettings").GetSection("SupportedUICultures").Get<string[]>() ?? null;
});
builder.Services.AddCryptographyClient();

var app = builder.BuildApp(useStreamableHttp);

using (var scope = app.Services.CreateScope())
{
}

if (useStreamableHttp)
{
    (app as WebApplication)!.MapOpenApi("/{documentName}.json");
}



await app.RunAsync();


