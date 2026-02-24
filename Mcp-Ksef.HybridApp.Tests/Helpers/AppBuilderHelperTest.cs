using McpKsef.HybridApp.Configurations;
using McpKsef.HybridApp.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Extensions;

namespace McpKsef.HybridApp.Tests.Helpers;

public class AppBuilderHelperTest
{
    private IHostApplicationBuilder CreateBuilder(string[] args = null)
    {
        args ??= Array.Empty<string>();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        
        var builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.AddConfiguration(config);
        return builder;
    }

    [Fact]
    public void Setup_WithoutStreamableHttp_ReturnsHostApplicationBuilder()
    {
        var args = Array.Empty<string>();

        var builder = AppBuilderHelper.Setup(false, args);

        Assert.NotNull(builder);
    }

    [Fact]
    public void Setup_WithStreamableHttp_ReturnsWebApplicationBuilder()
    {
        var args = Array.Empty<string>();

        var builder = AppBuilderHelper.Setup(true, args);

        Assert.NotNull(builder);
    }

    // [Fact]
    // public void Setup_RegistersAppSettings()
    // {
    //     var args = Array.Empty<string>();
    //
    //     var builder = AppBuilderHelper.Setup(false, args);
    //     var host = builder.BuildApp(true);
    //     var settings = host.Services.GetService(typeof(KsefAppSettings));
    //
    //     Assert.NotNull(settings);
    //     Assert.IsType<KsefAppSettings>(settings);
    // }

    [Fact]
    public void Setup_ConfiguresLogging()
    {
        var args = Array.Empty<string>();

        var builder = AppBuilderHelper.Setup(false, args);

        var loggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void Setup_WithStreamableHttpAddsHttpContextAccessor()
    {
        var args = Array.Empty<string>();

        var builder = AppBuilderHelper.Setup(true, args);

        var httpContextAccessor = builder.Services.BuildServiceProvider().GetService<IHttpContextAccessor>();
        Assert.NotNull(httpContextAccessor);
    }

    [Fact]
    public void Setup_AddsAppSettingsSingleton()
    {
        var args = Array.Empty<string>();

        var builder = AppBuilderHelper.Setup(false, args);
        var serviceProvider = builder.Services.BuildServiceProvider();
        
        var settings1 = serviceProvider.GetRequiredService<KsefAppSettings>();
        var settings2 = serviceProvider.GetRequiredService<KsefAppSettings>();

        Assert.Same(settings1, settings2);
    }

    // [Fact]
    // public void Setup_WithHttpArgument_PassedToAppSettings()
    // {
    //     var args = new[] { "--http" };
    //
    //     var builder = AppBuilderHelper.Setup(false, args);
    //     var serviceProvider = builder.Services.BuildServiceProvider();
    //     var settings = serviceProvider.GetRequiredService<KsefAppSettings>();
    //
    //     Assert.True(settings.UseHttp);
    // }
}
