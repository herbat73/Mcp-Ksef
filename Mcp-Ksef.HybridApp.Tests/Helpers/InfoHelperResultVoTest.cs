using McpKsef.HybridApp.Helpers;

namespace McpKsef.HybridApp.Tests.Helpers;

public class InfoHelperResultVoTest
{
    [Fact]
    public void IsValid_WithCertificateValidAndVatIdValid_ReturnsTrue()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefCertificateValid = true,
            IsVatIdValid = true
        };

        Assert.True(result.IsValid);
    }

    [Fact]
    public void IsValid_WithTokenValidAndVatIdValid_ReturnsTrue()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefTokenValid = true,
            IsVatIdValid = true
        };

        Assert.True(result.IsValid);
    }

    [Fact]
    public void IsValid_WithBothCertificateAndTokenValidAndVatIdValid_ReturnsTrue()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefCertificateValid = true,
            IsKsefTokenValid = true,
            IsVatIdValid = true
        };

        Assert.True(result.IsValid);
    }

    [Fact]
    public void IsValid_WithCertificateValidButVatIdInvalid_ReturnsFalse()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefCertificateValid = true,
            IsVatIdValid = false
        };

        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsValid_WithTokenValidButVatIdInvalid_ReturnsFalse()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefTokenValid = true,
            IsVatIdValid = false
        };

        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsValid_WithNeitherCertificateNorTokenAndVatIdValid_ReturnsFalse()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefCertificateValid = false,
            IsKsefTokenValid = false,
            IsVatIdValid = true
        };

        Assert.False(result.IsValid);
    }

    [Fact]
    public void IsValid_WithAllInvalid_ReturnsFalse()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefCertificateValid = false,
            IsKsefTokenValid = false,
            IsVatIdValid = false
        };

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DefaultInstance_AllPropertiesAreFalse()
    {
        var result = new InfoHelperResultVo();

        Assert.False(result.IsKsefCertificateValid);
        Assert.False(result.IsKsefTokenValid);
        Assert.False(result.IsVatIdValid);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CanSetAllPropertiesIndividually()
    {
        var result = new InfoHelperResultVo
        {
            IsKsefCertificateValid = true,
            IsKsefTokenValid = true,
            IsVatIdValid = true
        };

        Assert.True(result.IsKsefCertificateValid);
        Assert.True(result.IsKsefTokenValid);
        Assert.True(result.IsVatIdValid);
    }
}
