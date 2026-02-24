using Microsoft.Extensions.Configuration;
using Shared.Configurations;

namespace McpKsef.HybridApp.Tests.Configurations;

public class AppSettingsTest
{
    private IConfiguration CreateConfiguration(Dictionary<string, string> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return config;
    }

    [Fact]
    public void Parse_NoArgsWithDefaultConfig_ReturnsDefaultSettings()
    {
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new string[] { };

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.NotNull(settings);
        Assert.False(settings.UseHttp);
        Assert.False(settings.UseProduction);
        Assert.False(settings.Help);
    }

    //[Fact]
    // public void Parse_WithHttpArgument_SetsUseHttpTrue()
    // {
    //     var config = CreateConfiguration(new Dictionary<string, string>());
    //     var args = new[] { "--http" };
    //
    //     var settings = AppSettings.Parse<TestAppSettings>(config, args);
    //
    //     Assert.True(settings.UseHttp);
    // }

    // [Fact]
    // public void Parse_WithUseProductionArgument_SetsUseProductionTrue()
    // {
    //     var config = CreateConfiguration(new Dictionary<string, string>());
    //     var args = new[] { "--use-production" };
    //
    //     var settings = AppSettings.Parse<TestAppSettings>(config, args);
    //
    //     Assert.True(settings.UseProduction);
    // }

    [Fact]
    public void Parse_WithHelpArgument_SetsHelpTrue()
    {
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "--help" };

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.True(settings.Help);
    }

    [Fact]
    public void Parse_WithMultipleArguments_AppliesAllSettings()
    {
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "--http", "--use-production" };

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.True(settings.UseHttp);
        Assert.True(settings.UseProduction);
    }

    [Fact]
    public void Parse_WithUnknownArgument_SetsHelpTrue()
    {
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "--unknown" };

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.True(settings.Help);
    }

    [Fact]
    public void Parse_WithSingleArgument_SetsHelpTrue()
    {
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "single" };

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.True(settings.Help);
    }

    [Fact]
    public void Parse_ConfigBindsValuesToProperties()
    {
        var values = new Dictionary<string, string>
        {
            { "UseHttp", "true" },
            { "UseProduction", "true" }
        };
        var config = CreateConfiguration(values);
        var args = new string[] { };

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.True(settings.UseHttp);
        Assert.True(settings.UseProduction);
    }

    [Fact]
    public void UseStreamableHttp_WithHttpEnvironmentVariable_ReturnsTrue()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "true" } };
        var args = new string[] { };

        var result = AppSettings.UseStreamableHttp(env, args);

        Assert.True(result);
    }

    [Fact]
    public void UseStreamableHttp_WithHttpArgumentIgnoresEnv_ReturnsTrue()
    {
        var env = new System.Collections.Hashtable();
        var args = new[] { "--http" };

        var result = AppSettings.UseStreamableHttp(env, args);

        Assert.True(result);
    }

    [Fact]
    public void UseStreamableHttp_WithoutHttpConfigOrArgs_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable();
        var args = new string[] { };

        var result = AppSettings.UseStreamableHttp(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseStreamableHttp_WithFalseEnvironmentVariable_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "false" } };
        var args = new string[] { };

        var result = AppSettings.UseStreamableHttp(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseStreamableHttp_WithInvalidBoolValue_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "invalid" } };
        var args = new string[] { };

        var result = AppSettings.UseStreamableHttp(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseProductionServer_WithProductionEnvironmentVariable_ReturnsTrue()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "true" } };
        var args = new string[] { };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.True(result);
    }

    [Fact]
    public void UseProductionServer_WithProductionArgument_ReturnsTrue()
    {
        var env = new System.Collections.Hashtable();
        var args = new[] { "--use-ksef-production" };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.True(result);
    }

    [Fact]
    public void UseProductionServer_WithoutConfig_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable();
        var args = new string[] { };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseProductionServer_WithFalseEnvironmentVariable_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "false" } };
        var args = new string[] { };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseProductionServer_WithInvalidBoolValue_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "invalid" } };
        var args = new string[] { };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseProductionServer_ArgumentOverridesEnvironmentVariable()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "false" } };
        var args = new[] { "--use-ksef-production" };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.True(result);
    }

    private class TestAppSettings : AppSettings
    {
    }
}
