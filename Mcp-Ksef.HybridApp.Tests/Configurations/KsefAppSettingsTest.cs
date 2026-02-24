using McpKsef.HybridApp.Configurations;
using Microsoft.OpenApi.Models;
using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Configurations;

public class KsefAppSettingsTest
{
    [Fact]
    public void OpenApiInfo_IsInitializedWithAppConstants()
    {
        var settings = new KsefAppSettings();

        Assert.NotNull(settings.OpenApi);
        Assert.Equal(AppConsts.AppName, settings.OpenApi.Title);
        Assert.Equal(AppConsts.AppVersion, settings.OpenApi.Version);
        Assert.Equal(AppConsts.AppDescription, settings.OpenApi.Description);
    }

    [Fact]
    public void OpenApiInfo_CanBeOverridden()
    {
        var settings = new KsefAppSettings
        {
            OpenApi = new OpenApiInfo
            {
                Title = "Custom Title",
                Version = "2.0.0",
                Description = "Custom Description"
            }
        };

        Assert.Equal("Custom Title", settings.OpenApi.Title);
        Assert.Equal("2.0.0", settings.OpenApi.Version);
        Assert.Equal("Custom Description", settings.OpenApi.Description);
    }

    [Fact]
    public void InheritsFromAppSettings()
    {
        var settings = new KsefAppSettings();

        Assert.IsAssignableFrom<Shared.Configurations.AppSettings>(settings);
    }

    [Fact]
    public void UseHttpPropertyWorks()
    {
        var settings = new KsefAppSettings { UseHttp = true };

        Assert.True(settings.UseHttp);
    }

    [Fact]
    public void UseProductionPropertyWorks()
    {
        var settings = new KsefAppSettings { UseProduction = true };

        Assert.True(settings.UseProduction);
    }

    [Fact]
    public void HelpPropertyWorks()
    {
        var settings = new KsefAppSettings { Help = true };

        Assert.True(settings.Help);
    }
}
