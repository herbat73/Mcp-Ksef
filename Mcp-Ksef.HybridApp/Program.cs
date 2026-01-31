using KSeF.Client.DI;
using Shared.Configurations;
using Shared.Extensions;
using McpKsef.HybridApp.Helpers;
using Shared.Consts;

if (!RunInfoHelper.IsSettingsValidToRun()) return;

var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);
var builder = AppBuilderHelper.Setup(useStreamableHttp, args);

builder.Services.AddKSeFClient(options =>
{
    var ksefBaseUrl = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefBaseUrl);
    if (string.IsNullOrEmpty(ksefBaseUrl))
    {
        Console.WriteLine($"Environment setting {EnvironmentConsts.KsefBaseUrl} is not set. Use default Test {KsefEnvironmentsUris.TEST} as base KSeF API url");
        ksefBaseUrl = KsefEnvironmentsUris.TEST;
    }
    
    options.BaseUrl = ksefBaseUrl;
    
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


