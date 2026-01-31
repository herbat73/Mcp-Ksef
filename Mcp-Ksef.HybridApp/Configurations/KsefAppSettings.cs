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
}
