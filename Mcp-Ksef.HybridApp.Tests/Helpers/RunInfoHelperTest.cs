using McpKsef.HybridApp.Helpers;
using Shared.Consts;

namespace Mcp_Ksef.HybridApp.Tests.Helpers;

public class RunInfoHelperInfoTests
{
    private (InfoHelperResultVo result, string output) RunWithEnv(
        string? ksefValue,
        string? vatValue,
        bool createCertFiles = false,
        string? privateKeyPassword = null,
        string? certFilePath = null,
        string? keyFilePath = null)
    {
        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);

        var origKsef = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
        var origVat = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        var origCert = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile);
        var origKey = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile);
        var origPass = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword);

        string? createdCert = null;
        string? createdKey = null;

        try
        {
            if (createCertFiles)
            {
                createdCert = Path.GetTempFileName();
                File.WriteAllText(createdCert, "cert");
                createdKey = Path.GetTempFileName();
                File.WriteAllText(createdKey, "key");
            }

            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, ksefValue);
            Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, vatValue);

            if (createCertFiles)
            {
                Environment.SetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile, createdCert);
                Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile, createdKey);
            }
            else
            {
                Environment.SetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile, certFilePath);
                Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile, keyFilePath);
            }

            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword, privateKeyPassword);

            var result = RunInfoHelper.CheckEnvironmentConsts();
            Console.Out.Flush();
            return (result, sw.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, origKsef);
            Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, origVat);
            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile, origCert);
            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile, origKey);
            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword, origPass);

            if (createdCert != null && File.Exists(createdCert))
            {
                try { File.Delete(createdCert); } catch { }
            }
            if (createdKey != null && File.Exists(createdKey))
            {
                try { File.Delete(createdKey); } catch { }
            }

            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void BothTokenAndVatSet_ReturnsTokenAndVatValid_NoOutput()
    {
        var (result, output) = RunWithEnv("valid-token", "PL1234567890");
        Assert.True(result.IsKsefTokenValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(string.IsNullOrWhiteSpace(output));
    }

    [Fact]
    public void MissingVat_ReturnsVatInvalid_WritesVatMessage()
    {
        var (result, output) = RunWithEnv("valid-token", null);
        Assert.False(result.IsVatIdValid);
        Assert.Contains(EnvironmentConsts.VatId, output);
    }

    [Fact]
    public void MissingKsefToken_ButCertificateValid_ReturnsTokenInvalid_CertificateValid()
    {
        var (result, output) = RunWithEnv(
            ksefValue: null,
            vatValue: "PL1234567890",
            createCertFiles: true,
            privateKeyPassword: "pwd");

        Assert.False(result.IsKsefTokenValid);
        Assert.True(result.IsKsefCertificateValid);
    }

    [Fact]
    public void KsefCertificatePathsPointToMissingFiles_CertificateInvalid_WritesFilePathMessages()
    {
        var fakeCertPath = Path.Combine(Path.GetTempPath(), "nonexistent_cert_" + Guid.NewGuid() + ".crt");
        var fakeKeyPath = Path.Combine(Path.GetTempPath(), "nonexistent_key_" + Guid.NewGuid() + ".key");

        var (result, output) = RunWithEnv(
            ksefValue: null,
            vatValue: null,
            createCertFiles: false,
            privateKeyPassword: "pwd",
            certFilePath: fakeCertPath,
            keyFilePath: fakeKeyPath);

        Assert.False(result.IsKsefCertificateValid);
        Assert.Contains(EnvironmentConsts.KsefCertificateFile, output);
        Assert.Contains(EnvironmentConsts.KsefPrivateKeyFile, output);
    }

    [Fact]
    public void AllKsefParamsAndVatSet_ReturnsAllValid_NoOutput()
    {
        var (result, output) = RunWithEnv(
            ksefValue: "valid-token",
            vatValue: "PL1234567890",
            createCertFiles: true,
            privateKeyPassword: "pwd");

        Assert.True(result.IsKsefTokenValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(result.IsKsefCertificateValid);
        Assert.True(string.IsNullOrWhiteSpace(output));
    }

    [Fact]
    public void OnlyValidCertificate_NoTokenNoVat_CertificateValidOthersFalse()
    {
        var (result, output) = RunWithEnv(
            ksefValue: null,
            vatValue: null,
            createCertFiles: true,
            privateKeyPassword: "pwd");

        Assert.False(result.IsKsefTokenValid);
        Assert.False(result.IsVatIdValid);
        Assert.True(result.IsKsefCertificateValid);
    }

    [Fact]
    public void MissingPassword_CertificateInvalid()
    {
        var (result, output) = RunWithEnv(
            ksefValue: null,
            vatValue: null,
            createCertFiles: true,
            privateKeyPassword: null);

        Assert.False(result.IsKsefCertificateValid);
    }
}