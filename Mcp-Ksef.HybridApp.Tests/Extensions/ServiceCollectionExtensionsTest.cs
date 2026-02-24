using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Configurations;
using Shared.Extensions;

namespace McpKsef.HybridApp.Tests.Extensions;

public class ServiceCollectionExtensionsTest
{
    private IServiceCollection CreateServiceCollection()
    {
        return new ServiceCollection();
    }

    private IConfiguration CreateConfiguration(Dictionary<string, string> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return config;
    }

    [Fact]
    public void AddAppSettings_RegistersSettingsAsSingleton()
    {
        var services = CreateServiceCollection();
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new string[] { };

        services.AddAppSettings<TestAppSettings>(config, args);
        var serviceProvider = services.BuildServiceProvider();

        var settings1 = serviceProvider.GetRequiredService<TestAppSettings>();
        var settings2 = serviceProvider.GetRequiredService<TestAppSettings>();

        Assert.Same(settings1, settings2);
    }

    [Fact]
    public void AddAppSettings_ParsesConfigurationAndArgs()
    {
        var services = CreateServiceCollection();
        var config = CreateConfiguration(new Dictionary<string, string> { { "UseHttp", "true" } });
        var args = new[] { "--use-production" };

        services.AddAppSettings<TestAppSettings>(config, args);
        var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<TestAppSettings>();

        Assert.True(settings.UseHttp);
        Assert.True(settings.UseProduction);
    }

    [Fact]
    public void AddAppSettings_CreatesNewInstanceEachTime()
    {
        var services = CreateServiceCollection();
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new string[] { };

        services.AddAppSettings<TestAppSettings>(config, args);
        
        var provider1 = services.BuildServiceProvider();
        var provider2 = services.BuildServiceProvider();
        
        var settings1 = provider1.GetRequiredService<TestAppSettings>();
        var settings2 = provider2.GetRequiredService<TestAppSettings>();

        Assert.Same(settings1, settings1);
        Assert.NotSame(settings1, settings2);
    }

    [Fact]
    public void AddAppSettings_ReturnsServiceCollection()
    {
        var services = CreateServiceCollection();
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new string[] { };

        var result = services.AddAppSettings<TestAppSettings>(config, args);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddAppSettings_WithHelpFlag_SetsHelpTrue()
    {
        var services = CreateServiceCollection();
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "--help" };

        services.AddAppSettings<TestAppSettings>(config, args);
        var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<TestAppSettings>();

        Assert.True(settings.Help);
    }

    [Fact]
    public void AddAppSettings_WithHttpAndProductionFlags()
    {
        var services = CreateServiceCollection();
        var config = CreateConfiguration(new Dictionary<string, string>());
        var args = new[] { "--http", "--use-production" };

        services.AddAppSettings<TestAppSettings>(config, args);
        var serviceProvider = services.BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<TestAppSettings>();

        Assert.True(settings.UseHttp);
        Assert.True(settings.UseProduction);
    }

    private class TestAppSettings : AppSettings
    {
    }
}
