using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Shared.Consts;

namespace Shared.Configurations;

/// <summary>
/// This represents the base class for application settings.
/// </summary>
public abstract class AppSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to use HTTP for the MCP server or not.
    /// </summary>
    public bool UseHttp { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether to use KSeF production server or not.
    /// </summary>
    public bool UseProduction { get; set; }

    /// <summary>
    /// Gets or sets the OpenAPI configuration for the MCP server.
    /// </summary>
    public virtual OpenApiInfo OpenApi { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to display help information or not.
    /// </summary>
    public bool Help { get; set; }

    /// <summary>
    /// Parses the configuration settings and command line arguments for additional settings.
    /// </summary>
    /// <param name="config"><see cref="IConfiguration"/> instance</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <typeparam name="T">Type inheriting <see cref="AppSettings"/>.</typeparam>
    /// <returns>Returns an instance of <typeparamref name="T"/>.</returns>
    protected virtual T ParseMore<T>(IConfiguration config, string[] args) where T : AppSettings, new()
    {
        var settings = new T();
        config.Bind(settings);

        if (args.Length == 0)
        {
            return settings;
        }

        if (args.Length == 1)
        {
            settings.Help = true;
            return settings;
        }

        return settings;
    }

    /// <summary>
    /// Parses the configuration settings and command line arguments.
    /// </summary>
    /// <param name="config"><see cref="IConfiguration"/> instance</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <typeparam name="T">Type inheriting <see cref="AppSettings"/>.</typeparam>
    /// <returns>Returns an instance of <typeparamref name="T"/>.</returns>
    public static T Parse<T>(IConfiguration config, string[] args) where T : AppSettings, new()
    {
        var settings = new T();
        config.Bind(settings);

        settings = settings.ParseMore<T>(config, args);

        if (args.Length == 0)
        {
            return settings;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--http":
                    settings.UseHttp = true;
                    break;
                case "--use-production":
                    settings.UseProduction= true;
                    break;
                case "--help":
                default:
                    settings.Help = true;
                    break;
            }
        }

        return settings;
    }

    /// <summary>
    /// Checks whether to use streamable HTTP or not.
    /// </summary>
    /// <param name="env"><see cref="IDictionary"/> instance representing environment variables.</param>
    /// <param name="args">List of arguments passed from the command line.</param>
    /// <returns>Returns <c>True</c> if streamable HTTP is enabled; otherwise, <c>False</c>.</returns>
    public static bool UseStreamableHttp(IDictionary env, string[] args)
    {
        var useHttp = env.Contains("UseHttp") &&
                      bool.TryParse(env["UseHttp"]?.ToString()?.ToLowerInvariant(), out var result) && result;

        // An explicit --http / --http=true|false wins; otherwise the environment variable decides.
        // Unrelated arguments (e.g. --environment) do not switch the environment variable off.
        return GetFlag(args, HttpFlag) ?? useHttp;
    }

    /// <summary>
    /// Checks whether to use Ksef production server or not.
    /// </summary>
    public static bool UseProductionServer(IDictionary env, string[] args)
    {
        var useProduction = env.Contains(EnvironmentConsts.UseKsefProduction) &&
                      bool.TryParse(env[EnvironmentConsts.UseKsefProduction]?.ToString()?.ToLowerInvariant(), out var result) && result;

        // An explicit --use-ksef-production / --use-ksef-production=true|false wins; otherwise the
        // environment variable decides. Unrelated arguments do not switch the environment variable off.
        return GetFlag(args, UseProductionFlag) ?? useProduction;
    }

    /// <summary>Command-line flag that selects the streamable HTTP transport.</summary>
    public const string HttpFlag = "--http";

    /// <summary>Command-line flag that selects the KSeF production server.</summary>
    public const string UseProductionFlag = "--use-ksef-production";

    private static readonly string[] AppFlags = [HttpFlag, UseProductionFlag];

    /// <summary>
    /// Reads an app flag from the command line: <c>--flag</c> means true, <c>--flag=true|false</c> sets the value.
    /// The last occurrence wins. Returns null when the flag is absent or its value is not a boolean.
    /// </summary>
    public static bool? GetFlag(string[] args, string flag)
    {
        bool? value = null;
        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
            {
                value = true;
            }
            else if (arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = bool.TryParse(arg[(flag.Length + 1)..], out var parsed) ? parsed : null;
            }
        }
        return value;
    }

    /// <summary>
    /// Removes the app's own flags (<see cref="HttpFlag"/>, <see cref="UseProductionFlag"/>) from the arguments.
    /// .NET configuration reads <c>--key value</c> pairs, so a valueless flag would otherwise swallow the next
    /// argument: <c>--http --environment Development</c> would lose <c>--environment</c>. Pass the result to the
    /// host builder and to anything else that parses arguments with .NET configuration.
    /// </summary>
    public static string[] RemoveAppFlags(string[] args) =>
        args.Where(arg => !AppFlags.Any(flag =>
                string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
}
