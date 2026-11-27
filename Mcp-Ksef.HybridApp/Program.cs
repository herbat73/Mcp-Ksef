using KSeF.Client.DI;
using Shared.Configurations;
using Shared.Extensions;
using McpKsef.HybridApp.Helpers;
using McpKsef.HybridApp.Services;
using Shared.Consts;

// The transport decides which host runs, and so which variable sets its environment.
var useStreamableHttp = AppSettings.UseStreamableHttp(Environment.GetEnvironmentVariables(), args);

// In Development, KSeF settings can come from user secrets (dotnet user-secrets) instead of launchSettings.json.
UserSecretsHelper.LoadIntoEnvironmentIfDevelopment(typeof(Program).Assembly, useStreamableHttp, args);

if (!RunInfoHelper.CheckEnvironmentConsts().IsValid) return;

// Startup messages go to stderr: in stdio mode stdout is the MCP JSON-RPC channel.
Console.Error.WriteLine($"Starting {AppConsts.AppName} for VatId : {Environment.GetEnvironmentVariable(EnvironmentConsts.VatId)}");

var builder = AppBuilderHelper.Setup(useStreamableHttp, args);

builder.Services.AddKSeFClient(options =>
{
    var useProductionServer = AppSettings.UseProductionServer(Environment.GetEnvironmentVariables(), args);
    options.BaseUrl = useProductionServer ? KsefEnvironmentsUris.PROD : KsefEnvironmentsUris.TEST;
    var serverName = useProductionServer ? "Serwer produkcyjny" : "Serwer testowy";
    Console.Error.WriteLine($"{serverName} - KSeF API URL: {options.BaseUrl}");
    
    options.CustomHeaders =
        builder.Configuration
            .GetSection("ApiSettings:customHeaders")
            .Get<Dictionary<string, string>>()
        ?? new Dictionary<string, string>();
    
    options.ResourcesPath = (builder.Configuration.GetSection("ApiSettings")
        .GetValue<string>("ResourcesPath") ?? null)!;
    
    options.DefaultCulture = (builder.Configuration.GetSection("ApiSettings")
        .GetValue<string>("DefaultCulture") ?? null)!;
    
    options.SupportedCultures = (builder.Configuration.GetSection("ApiSettings").GetSection("SupportedCultures").Get<string[]>() ?? null)!;
    options.SupportedUICultures = (builder.Configuration.GetSection("ApiSettings").GetSection("SupportedUICultures").Get<string[]>() ?? null)!;
});

builder.Services.AddCryptographyClient();
builder.Services.AddSingleton<AuthenticationResponse>();
builder.Services.AddScoped<IKsefAuthorizationService, KsefAuthorizationService>();

var app = builder.BuildApp(useStreamableHttp);

if (useStreamableHttp)
{
    (app as WebApplication)!.MapOpenApi("/{documentName}.json");
}

await app.RunAsync();


