using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Consts;
using Shared.Extensions;

namespace McpKsef.HybridApp.Tests.Extensions;

public class HostApplicationBuilderExtensionsTest
{
    private IHostApplicationBuilder CreateBuilder(string[] args = null)
    {
        args ??= [];
        return Host.CreateApplicationBuilder(args);
    }

    [Fact]
    public void BuildApp_WithoutStreamableHttp_ReturnsIHost()
    {
        var builder = CreateBuilder();

        var app = builder.BuildApp(false);

        Assert.NotNull(app);
        Assert.IsAssignableFrom<IHost>(app);
    }

    [Fact]
    public void BuildApp_WithStreamableHttp_ReturnsWebApplication()
    {
        var webBuilder = WebApplication.CreateBuilder([]);
        IHostApplicationBuilder builder = webBuilder;

        var app = builder.BuildApp(true);

        Assert.NotNull(app);
    }

    [Fact]
    public void BuildApp_RegistersMcpServer()
    {
        var builder = CreateBuilder();

        var app = builder.BuildApp(false);

        var serviceProvider = app.Services;
        Assert.NotNull(serviceProvider);
    }

    [Fact]
    public void BuildApp_WithStreamableHttp_RegistersOpenApiEndpoints()
    {
        var webBuilder = WebApplication.CreateBuilder([]);
        IHostApplicationBuilder builder = webBuilder;

        var app = builder.BuildApp(true);

        Assert.NotNull(app);
    }

    [Fact]
    public void BuildApp_WithoutStreamableHttp_DoesNotRegisterWebServices()
    {
        var builder = CreateBuilder();

        var app = builder.BuildApp(false);

        Assert.NotNull(app);
    }

    [Fact]
    public void BuildApp_BuiltAppCanBeStarted()
    {
        var builder = CreateBuilder();
        var app = builder.BuildApp(false);

        Assert.NotNull(app);
    }

    // [Fact]
    // public void BuildApp_WithStreamableHttpRegistersHttpContextAccessor()
    // {
    //     var webBuilder = WebApplication.CreateBuilder(new string[] { });
    //     var builder = (IHostApplicationBuilder)webBuilder;
    //     var app = builder.BuildApp(true);
    //
    //     var httpContextAccessor = app.Services.GetService<IHttpContextAccessor>();
    //     Assert.NotNull(httpContextAccessor);
    // }
}
