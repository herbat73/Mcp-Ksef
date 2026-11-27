using System.Reflection;
using Microsoft.Extensions.Configuration;
using Shared.Consts;

namespace McpKsef.HybridApp.Helpers;

/// <summary>
/// Makes KSeF settings stored in .NET user secrets visible as environment variables.
/// The app reads its KSeF settings with <see cref="Environment.GetEnvironmentVariable(string)"/>
/// before the host is built, so user secrets have to be copied into the process environment.
/// </summary>
public static class UserSecretsHelper
{
    private static readonly string[] KsefKeys =
    [
        EnvironmentConsts.KsefToken,
        EnvironmentConsts.VatId,
        EnvironmentConsts.UseKsefProduction,
        EnvironmentConsts.KsefCertificateFile,
        EnvironmentConsts.KsefPrivateKeyFile,
        EnvironmentConsts.KsefPrivateKeyPassword
    ];

    /// <summary>
    /// In the Development environment, loads the user secrets of <paramref name="assembly"/>
    /// and copies the KSeF settings into environment variables. Does nothing in other environments.
    /// </summary>
    public static IReadOnlyList<string> LoadIntoEnvironmentIfDevelopment(Assembly assembly)
    {
        if (!string.Equals(GetEnvironmentName(), "Development", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var secrets = new ConfigurationBuilder()
            .AddUserSecrets(assembly, optional: true)
            .Build();
        return CopyKsefSettingsToEnvironment(secrets);
    }

    /// <summary>
    /// Returns the environment name the same way the ASP.NET Core host resolves it:
    /// ASPNETCORE_ENVIRONMENT wins over DOTNET_ENVIRONMENT, and empty values count as unset.
    /// </summary>
    public static string? GetEnvironmentName()
    {
        var aspNetCore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(aspNetCore))
        {
            return aspNetCore;
        }

        var dotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.IsNullOrWhiteSpace(dotnet) ? null : dotnet;
    }

    /// <summary>
    /// Copies non-empty KSeF settings from <paramref name="configuration"/> into environment variables.
    /// Variables that are already set are left unchanged, so real environment variables take precedence.
    /// </summary>
    /// <returns>The names of the variables that were set.</returns>
    public static IReadOnlyList<string> CopyKsefSettingsToEnvironment(IConfiguration configuration)
    {
        var copied = new List<string>();
        foreach (var key in KsefKeys)
        {
            var value = configuration[key];
            if (string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
            copied.Add(key);
        }
        return copied;
    }
}
