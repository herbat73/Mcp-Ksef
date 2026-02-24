using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Consts;

public class AppConstsTest
{
    [Fact]
    public void AppNameConstant_HasCorrectValue()
    {
        Assert.Equal("MCP KSeF", AppConsts.AppName);
    }

    [Fact]
    public void AppVersionConstant_HasCorrectValue()
    {
        Assert.Equal("1.0.0", AppConsts.AppVersion);
    }

    [Fact]
    public void AppDescriptionConstant_HasCorrectValue()
    {
        Assert.Equal("MCP server for connecting KSeF repository", AppConsts.AppDescription);
    }

    [Fact]
    public void KsefToolNameConstant_HasCorrectValue()
    {
        Assert.Equal("KSeF Tools", AppConsts.KsefToolName);
    }

    [Fact]
    public void AllConstantsAreNonEmpty()
    {
        Assert.NotEmpty(AppConsts.AppName);
        Assert.NotEmpty(AppConsts.AppVersion);
        Assert.NotEmpty(AppConsts.AppDescription);
        Assert.NotEmpty(AppConsts.KsefToolName);
    }

    [Fact]
    public void AppVersionFollowsSemanticVersioning()
    {
        var version = AppConsts.AppVersion;
        var parts = version.Split('.');
        
        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out _));
        Assert.True(int.TryParse(parts[1], out _));
        Assert.True(int.TryParse(parts[2], out _));
    }

    [Fact]
    public void AllConstantsAreUnique()
    {
        var constants = new[]
        {
            AppConsts.AppName,
            AppConsts.AppVersion,
            AppConsts.AppDescription,
            AppConsts.KsefToolName
        };

        Assert.Equal(constants.Length, constants.Distinct().Count());
    }
}
