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
    /// <param name="assembly">Assembly whose UserSecretsId identifies the secrets.</param>
    /// <param name="useStreamableHttp">True for the HTTP (WebApplication) host, false for the stdio (generic) host.</param>
    /// <param name="args">Command-line arguments, which can set the environment with --environment.</param>
    public static IReadOnlyList<string> LoadIntoEnvironmentIfDevelopment(Assembly assembly, bool useStreamableHttp, string[] args)
    {
        if (!string.Equals(GetEnvironmentName(useStreamableHttp, args), "Development", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var secrets = new ConfigurationBuilder()
            .AddUserSecrets(assembly, optional: true)
            .Build();
        return CopyKsefSettingsToEnvironment(secrets);
    }

    /// <summary>
    /// Returns the environment name the same way the host that will run resolves it:
    /// <list type="bullet">
    /// <item>--environment on the command line wins for both hosts;</item>
    /// <item>the HTTP host (WebApplication) then uses ASPNETCORE_ENVIRONMENT, then DOTNET_ENVIRONMENT;</item>
    /// <item>the stdio host (Host.CreateApplicationBuilder) uses only DOTNET_ENVIRONMENT.</item>
    /// </list>
    /// A variable that is set but empty still overrides the lower-priority one, as in the host, and means
    /// "not Development". Returns null when no name applies (the host then runs as Production).
    /// Note: on Windows a variable set to an empty string does not exist at all, so it never overrides.
    /// </summary>
    public static string? GetEnvironmentName(bool useStreamableHttp, string[] args)
    {
        var fromArgs = new ConfigurationBuilder().AddCommandLine(args).Build()["environment"];
        if (!string.IsNullOrWhiteSpace(fromArgs))
        {
            return fromArgs;
        }

        if (useStreamableHttp)
        {
            var aspNetCore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (aspNetCore != null)
            {
                return NullIfBlank(aspNetCore);
            }
        }

        return NullIfBlank(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
