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
        var args = Array.Empty<string>();

        var settings = AppSettings.Parse<TestAppSettings>(config, args);

        Assert.NotNull(settings);
        Assert.False(settings.UseHttp);
        Assert.False(settings.UseProduction);
        Assert.False(settings.Help);
    }

    [Fact]
     public void Parse_WithHttpArgument_SetsUseHttpTrue()
     {
         var config = CreateConfiguration(new Dictionary<string, string>());
         var args = new[] { "--http" };
    
         var settings = AppSettings.Parse<TestAppSettings>(config, args);
    
         Assert.True(settings.UseHttp);
     }

    [Fact]
    public void Parse_WithUseProductionArgument_SetsUseProductionTrue()
    {
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "--use-production" };
    
        var settings = AppSettings.Parse<TestAppSettings>(config, args);
    
        Assert.True(settings.UseProduction);
    }

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
    public void UseStreamableHttp_EnvironmentVariableWithUnrelatedArguments_ReturnsTrue()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "true" } };
        var args = new[] { "--environment", "Development" };

        var result = AppSettings.UseStreamableHttp(env, args);

        Assert.True(result);
    }

    [Fact]
    public void UseProductionServer_EnvironmentVariableWithUnrelatedArguments_ReturnsTrue()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "true" } };
        var args = new[] { "--environment", "Development" };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.True(result);
    }

    [Fact]
    public void UseProductionServer_UnrelatedArgumentsWithoutConfig_ReturnsFalse()
    {
        var env = new System.Collections.Hashtable();
        var args = new[] { "--environment", "Development" };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.False(result);
    }

    [Fact]
    public void UseProductionServer_ExplicitFalseFlag_OverridesEnvironmentVariable()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "true" } };
        var args = new[] { "--use-ksef-production=false" };

        Assert.False(AppSettings.UseProductionServer(env, args));
    }

    [Fact]
    public void UseStreamableHttp_ExplicitFalseFlag_OverridesEnvironmentVariable()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "true" } };
        var args = new[] { "--http=false" };

        Assert.False(AppSettings.UseStreamableHttp(env, args));
    }

    [Fact]
    public void UseStreamableHttp_ExplicitTrueFlag_OverridesEnvironmentVariable()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "false" } };
        var args = new[] { "--http=true" };

        Assert.True(AppSettings.UseStreamableHttp(env, args));
    }

    [Fact]
    public void UseStreamableHttp_InvalidFlagValue_FallsBackToEnvironmentVariable()
    {
        var env = new System.Collections.Hashtable { { "UseHttp", "true" } };
        var args = new[] { "--http=maybe" };

        Assert.True(AppSettings.UseStreamableHttp(env, args));
    }

    [Theory]
    [InlineData(new[] { "--http" }, true)]
    [InlineData(new[] { "--HTTP" }, true)]
    [InlineData(new[] { "--http=false" }, false)]
    [InlineData(new[] { "--http", "--http=false" }, false)]   // last occurrence wins
    [InlineData(new[] { "--http=false", "--http" }, true)]
    [InlineData(new[] { "--environment", "Development" }, null)]
    [InlineData(new string[0], null)]
    public void GetFlag_ReadsFlagFromArguments(string[] args, bool? expected)
    {
        Assert.Equal(expected, AppSettings.GetFlag(args, AppSettings.HttpFlag));
    }

    [Fact]
    public void RemoveAppFlags_KeepsOtherArgumentsInOrder()
    {
        var args = new[] { "--http", "--environment", "Development", "--use-ksef-production=true", "--Use-Ksef-Production", "--other=1" };

        Assert.Equal(new[] { "--environment", "Development", "--other=1" }, AppSettings.RemoveAppFlags(args));
    }

    [Fact]
    public void UseProductionServer_ArgumentOverridesEnvironmentVariable()
    {
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "false" } };
        var args = new[] { "--use-ksef-production" };

        var result = AppSettings.UseProductionServer(env, args);

        Assert.True(result);
    }

    [Theory]
    [InlineData(new[] { "--use-ksef-production", "false" }, false)]
    [InlineData(new[] { "--use-ksef-production", "FALSE" }, false)]
    [InlineData(new[] { "--use-ksef-production", "true" }, true)]
    [InlineData(new[] { "--use-ksef-production", "--environment", "Development" }, true)]
    [InlineData(new[] { "--use-ksef-production", "Development" }, true)]   // a non-boolean next argument is not the value
    public void GetFlag_SpaceSeparatedValue_IsRead(string[] args, bool expected)
    {
        Assert.Equal(expected, AppSettings.GetFlag(args, AppSettings.UseProductionFlag));
    }

    [Fact]
    public void UseProductionServer_SpaceSeparatedFalse_DoesNotSelectProduction()
    {
        // Regression: "--use-ksef-production false" used to select the production server.
        var env = new System.Collections.Hashtable { { "KSEF_USEPRODUCTIONSERVER", "true" } };

        Assert.False(AppSettings.UseProductionServer(env, new[] { "--use-ksef-production", "false" }));
        Assert.False(AppSettings.UseProductionServer(new System.Collections.Hashtable(), new[] { "--use-ksef-production", "false" }));
    }

    [Theory]
    [InlineData(new[] { "--use-ksef-production", "false", "--environment", "Development" }, new[] { "--environment", "Development" })]
    [InlineData(new[] { "--http", "true" }, new string[0])]
    [InlineData(new[] { "--use-production", "--environment=Development" }, new[] { "--environment=Development" })]
    [InlineData(new[] { "--http", "Development" }, new[] { "Development" })]   // not a boolean, so not the flag's value
    public void RemoveAppFlags_AlsoRemovesSpaceSeparatedBooleanValue(string[] args, string[] expected)
    {
        Assert.Equal(expected, AppSettings.RemoveAppFlags(args));
    }

    [Fact]
    public void Parse_SingleKnownFlag_IsNotHelp()
    {
        var settings = AppSettings.Parse<TestAppSettings>(CreateConfiguration(new Dictionary<string, string>()), new[] { "--http" });

        Assert.True(settings.UseHttp);
        Assert.False(settings.Help);
    }

    [Theory]
    [InlineData(new[] { "--http=true", "--environment", "Development" }, true, false)]
    [InlineData(new[] { "--http", "false" }, false, false)]
    [InlineData(new[] { "--use-ksef-production" }, false, true)]
    [InlineData(new[] { "--use-ksef-production=true", "--environment=Development" }, false, true)]
    [InlineData(new[] { "--use-production" }, false, true)]
    public void Parse_UnderstandsFlagFormsAndEnvironment(string[] args, bool useHttp, bool useProduction)
    {
        var settings = AppSettings.Parse<TestAppSettings>(CreateConfiguration(new Dictionary<string, string>()), args);

        Assert.Equal(useHttp, settings.UseHttp);
        Assert.Equal(useProduction, settings.UseProduction);
        Assert.False(settings.Help);
    }

    [Fact]
    public void Parse_UnknownArgumentAmongKnownOnes_SetsHelp()
    {
        var settings = AppSettings.Parse<TestAppSettings>(CreateConfiguration(new Dictionary<string, string>()),
            new[] { "--http", "--environment", "Development", "--bogus" });

        Assert.True(settings.UseHttp);
        Assert.True(settings.Help);
    }

    private class TestAppSettings : AppSettings
    {
    }
}
