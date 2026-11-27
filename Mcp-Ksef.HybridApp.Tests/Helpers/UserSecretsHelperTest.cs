using McpKsef.HybridApp.Helpers;
using Microsoft.Extensions.Configuration;
using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Helpers;

[Collection(EnvironmentVariablesCollection.Name)]
public class UserSecretsHelperTest : IDisposable
{
    private static readonly string[] Keys =
    [
        EnvironmentConsts.KsefToken,
        EnvironmentConsts.VatId,
        EnvironmentConsts.UseKsefProduction,
        EnvironmentConsts.KsefCertificateFile,
        EnvironmentConsts.KsefPrivateKeyFile,
        EnvironmentConsts.KsefPrivateKeyPassword,
        "DOTNET_ENVIRONMENT",
        "ASPNETCORE_ENVIRONMENT"
    ];

    private readonly Dictionary<string, string?> _original;

    public UserSecretsHelperTest()
    {
        _original = Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public void Dispose()
    {
        foreach (var (key, value) in _original)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static void SetEnvironment(string? aspNetCore, string? dotnet)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", aspNetCore);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", dotnet);
    }

    [Fact]
    public void CopyKsefSettingsToEnvironment_SetsMissingVariables()
    {
        var copied = UserSecretsHelper.CopyKsefSettingsToEnvironment(Config(new()
        {
            [EnvironmentConsts.VatId] = "1234567890",
            [EnvironmentConsts.KsefPrivateKeyPassword] = "secret"
        }));

        Assert.Equal("1234567890", Environment.GetEnvironmentVariable(EnvironmentConsts.VatId));
        Assert.Equal("secret", Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword));
        Assert.Equal([EnvironmentConsts.VatId, EnvironmentConsts.KsefPrivateKeyPassword], copied);
    }

    [Fact]
    public void CopyKsefSettingsToEnvironment_DoesNotOverrideExistingVariables()
    {
        Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, "from-environment");

        var copied = UserSecretsHelper.CopyKsefSettingsToEnvironment(Config(new()
        {
            [EnvironmentConsts.VatId] = "from-secrets"
        }));

        Assert.Equal("from-environment", Environment.GetEnvironmentVariable(EnvironmentConsts.VatId));
        Assert.Empty(copied);
    }

    [Fact]
    public void CopyKsefSettingsToEnvironment_SkipsEmptyValues()
    {
        var copied = UserSecretsHelper.CopyKsefSettingsToEnvironment(Config(new()
        {
            [EnvironmentConsts.KsefToken] = ""
        }));

        Assert.Null(Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken));
        Assert.Empty(copied);
    }

    [Fact]
    public void CopyKsefSettingsToEnvironment_IgnoresNonKsefKeys()
    {
        var copied = UserSecretsHelper.CopyKsefSettingsToEnvironment(Config(new()
        {
            ["SOME_OTHER_SETTING"] = "value"
        }));

        Assert.Null(Environment.GetEnvironmentVariable("SOME_OTHER_SETTING"));
        Assert.Empty(copied);
    }

    // HTTP host (WebApplication): ASPNETCORE_ENVIRONMENT wins over DOTNET_ENVIRONMENT.
    [Theory]
    [InlineData("Production", "Development", "Production")]
    [InlineData(null, "Development", "Development")]
    // A set-but-blank ASPNETCORE_ENVIRONMENT still overrides DOTNET_ENVIRONMENT, as in the host, and is not Development.
    [InlineData("", "Development", null)]
    [InlineData(" ", "Development", null)]
    [InlineData("Development", "", "Development")]
    [InlineData(null, "", null)]
    [InlineData(null, null, null)]
    public void GetEnvironmentName_HttpHost_PrefersAspNetCoreVariable(string? aspNetCore, string? dotnet, string? expected)
    {
        SetEnvironment(aspNetCore, dotnet);

        Assert.Equal(expected, UserSecretsHelper.GetEnvironmentName(useStreamableHttp: true, []));
    }

    // stdio host (Host.CreateApplicationBuilder): only DOTNET_ENVIRONMENT counts.
    [Theory]
    [InlineData("Production", "Development", "Development")]
    [InlineData("Development", null, null)]
    [InlineData("Development", "", null)]
    [InlineData(null, "Development", "Development")]
    public void GetEnvironmentName_StdioHost_IgnoresAspNetCoreVariable(string? aspNetCore, string? dotnet, string? expected)
    {
        SetEnvironment(aspNetCore, dotnet);

        Assert.Equal(expected, UserSecretsHelper.GetEnvironmentName(useStreamableHttp: false, []));
    }

    [Theory]
    [InlineData(true, new[] { "--environment", "Development" })]
    [InlineData(false, new[] { "--environment", "Development" })]
    [InlineData(false, new[] { "--environment=Development" })]
    [InlineData(true, new[] { "--environment=Development", "--http", "true" })]
    public void GetEnvironmentName_CommandLineArgumentWinsOverVariables(bool useStreamableHttp, string[] args)
    {
        SetEnvironment("Production", "Production");

        Assert.Equal("Development", UserSecretsHelper.GetEnvironmentName(useStreamableHttp, args));
    }

    // .NET configuration would read "--http --environment X" as http="--environment", losing the environment;
    // the app's own flags are removed first, as AppBuilderHelper does for the host.
    [Theory]
    [InlineData(true, new[] { "--http", "--environment", "Development" })]
    [InlineData(false, new[] { "--use-ksef-production", "--environment", "Development" })]
    [InlineData(true, new[] { "--http=true", "--environment", "Development" })]
    public void GetEnvironmentName_EnvironmentAfterAppFlag_IsRead(bool useStreamableHttp, string[] args)
    {
        SetEnvironment("Production", "Production");

        Assert.Equal("Development", UserSecretsHelper.GetEnvironmentName(useStreamableHttp, args));
    }

    [Fact]
    public void CopyKsefSettingsToEnvironment_ExistingEmptyVariable_IsNotOverwritten()
    {
        // e.g. KSEF_TOKEN= set on purpose to force certificate authentication.
        Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, "");

        var copied = UserSecretsHelper.CopyKsefSettingsToEnvironment(Config(new()
        {
            [EnvironmentConsts.KsefToken] = "token-from-secrets"
        }));

        Assert.Empty(copied);
        Assert.Equal("", Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken));
    }

    [Fact]
    public void CopyKsefSettingsToEnvironment_ExistingBlankVariable_IsNotOverwritten()
    {
        Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, " ");

        var copied = UserSecretsHelper.CopyKsefSettingsToEnvironment(Config(new()
        {
            [EnvironmentConsts.KsefToken] = "token-from-secrets"
        }));

        Assert.Empty(copied);
        Assert.Equal(" ", Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken));
    }

    [Fact]
    public void LoadIntoEnvironmentIfDevelopment_StdioWithOnlyAspNetCoreDevelopment_DoesNothing()
    {
        // The stdio host would run as Production here, so developer secrets must not be loaded.
        SetEnvironment("Development", null);

        var copied = UserSecretsHelper.LoadIntoEnvironmentIfDevelopment(typeof(UserSecretsHelper).Assembly, useStreamableHttp: false, []);

        Assert.Empty(copied);
        Assert.Null(Environment.GetEnvironmentVariable(EnvironmentConsts.VatId));
    }

    [Fact]
    public void LoadIntoEnvironmentIfDevelopment_HttpWithAspNetCoreProduction_DoesNothing()
    {
        SetEnvironment("Production", "Development");

        var copied = UserSecretsHelper.LoadIntoEnvironmentIfDevelopment(typeof(UserSecretsHelper).Assembly, useStreamableHttp: true, []);

        Assert.Empty(copied);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Production")]
    public void LoadIntoEnvironmentIfDevelopment_OutsideDevelopment_DoesNothing(string? environmentName)
    {
        SetEnvironment(environmentName, environmentName);

        var copied = UserSecretsHelper.LoadIntoEnvironmentIfDevelopment(typeof(UserSecretsHelper).Assembly, useStreamableHttp: true, []);

        Assert.Empty(copied);
        Assert.Null(Environment.GetEnvironmentVariable(EnvironmentConsts.VatId));
    }
}
