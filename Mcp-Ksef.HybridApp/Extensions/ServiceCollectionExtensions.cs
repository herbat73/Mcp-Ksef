using System.Text.RegularExpressions;
using McpKsef.HybridApp.Configurations;
using Shared.Extensions;

namespace McpKsef.HybridApp.Extensions;

/// <summary>
/// This represents the extension entity for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the application settings to the service collection.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/> instance.</param>
    /// <param name="config"><see cref="IConfiguration"/> instance.</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <returns>Returns the <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddAppSettings(this IServiceCollection services, IConfiguration config, string[] args)
    {
        services.AddAppSettings<KsefAppSettings>(config, args);

        services.AddSingleton<Regex>(sp =>
        {
            var regex = new Regex("\\<pre\\>\\<code class=\"language\\-(.+)\"\\>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            return regex;
        });

        return services;
    }
}
