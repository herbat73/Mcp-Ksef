using KSeF.Client.Api.Services;
using KSeF.Client.Api.Services.Internal;
using KSeF.Client.Clients;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.DI;
using Shared.Configurations;
using Shared.Extensions;
using McpKsef.HybridApp.Helpers;
using Shared.Consts;

if (!RunInfoHelper.IsSettingsValidToRun()) return;

Console.WriteLine($"Starting {AppConsts.AppName} for VatId : {Environment.GetEnvironmentVariable(EnvironmentConsts.VatId)}");

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

builder.Services.AddSingleton<ICryptographyClient, CryptographyClient>();
builder.Services.AddSingleton<ICertificateFetcher, DefaultCertificateFetcher>();
builder.Services.AddSingleton<ICryptographyService, CryptographyService>();
builder.Services.AddSingleton<CryptographyWarmupHostedService>();

var app = builder.BuildApp(useStreamableHttp);

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CryptographyWarmupHostedService>()
        .StartAsync(CancellationToken.None).GetAwaiter().GetResult();
}

if (useStreamableHttp)
{
    (app as WebApplication)!.MapOpenApi("/{documentName}.json");
}



await app.RunAsync();


