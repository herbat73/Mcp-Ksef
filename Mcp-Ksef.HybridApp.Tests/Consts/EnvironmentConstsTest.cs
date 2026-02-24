using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Consts;

public class EnvironmentConstsTest
{
    [Fact]
    public void KsefTokenConstant_HasCorrectValue()
    {
        Assert.Equal("KSEF_TOKEN", EnvironmentConsts.KsefToken);
    }

    [Fact]
    public void VatIdConstant_HasCorrectValue()
    {
        Assert.Equal("KSEF_VATID", EnvironmentConsts.VatId);
    }

    [Fact]
    public void UseKsefProductionConstant_HasCorrectValue()
    {
        Assert.Equal("KSEF_USEPRODUCTIONSERVER", EnvironmentConsts.UseKsefProduction);
    }

    [Fact]
    public void KsefCertificateFileConstant_HasCorrectValue()
    {
        Assert.Equal("KSEF_CERTIFICATE_FILE", EnvironmentConsts.KsefCertificateFile);
    }

    [Fact]
    public void KsefPrivateKeyFileConstant_HasCorrectValue()
    {
        Assert.Equal("KSEF_PRIVATE_KEY_FILE", EnvironmentConsts.KsefPrivateKeyFile);
    }

    [Fact]
    public void KsefPrivateKeyPasswordConstant_HasCorrectValue()
    {
        Assert.Equal("KSEF_PRIVATE_KEY_PASSWORD", EnvironmentConsts.KsefPrivateKeyPassword);
    }

    [Fact]
    public void AllConstantsAreNonEmpty()
    {
        Assert.NotEmpty(EnvironmentConsts.KsefToken);
        Assert.NotEmpty(EnvironmentConsts.VatId);
        Assert.NotEmpty(EnvironmentConsts.UseKsefProduction);
        Assert.NotEmpty(EnvironmentConsts.KsefCertificateFile);
        Assert.NotEmpty(EnvironmentConsts.KsefPrivateKeyFile);
        Assert.NotEmpty(EnvironmentConsts.KsefPrivateKeyPassword);
    }

    [Fact]
    public void AllConstantsAreUnique()
    {
        var constants = new[]
        {
            EnvironmentConsts.KsefToken,
            EnvironmentConsts.VatId,
            EnvironmentConsts.UseKsefProduction,
            EnvironmentConsts.KsefCertificateFile,
            EnvironmentConsts.KsefPrivateKeyFile,
            EnvironmentConsts.KsefPrivateKeyPassword
        };

        Assert.Equal(constants.Length, constants.Distinct().Count());
    }
}
