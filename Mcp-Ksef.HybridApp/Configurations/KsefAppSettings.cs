using Shared.Configurations;
using Microsoft.OpenApi.Models;
using Shared.Consts;

namespace McpKsef.HybridApp.Configurations;

/// <summary>
/// This represents the application settings for KSeF app.
/// </summary>
public class KsefAppSettings : AppSettings
{
    /// <inheritdoc />
    public override OpenApiInfo OpenApi { get; set; } = new()
    {
        Title = AppConsts.AppName,
        Version = AppConsts.AppVersion,
        Description = AppConsts.AppDescription
    };
    
    /// <inheritdoc />
    protected override T ParseMore<T>(IConfiguration config, string[] args)
    {
        var settings = base.ParseMore<T>(config, args);

        foreach (var arg in args)
        {
            switch (arg)
            {
                default:
                    settings.Help = true;
                    break;
            }
        }
        return settings;
    }
}