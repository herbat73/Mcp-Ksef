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

    [Theory]
    [InlineData(null)]
    [InlineData("Production")]
    public void LoadIntoEnvironmentIfDevelopment_OutsideDevelopment_DoesNothing(string? environmentName)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environmentName);

        var copied = UserSecretsHelper.LoadIntoEnvironmentIfDevelopment(typeof(UserSecretsHelper).Assembly);

        Assert.Empty(copied);
        Assert.Null(Environment.GetEnvironmentVariable(EnvironmentConsts.VatId));
    }
}
