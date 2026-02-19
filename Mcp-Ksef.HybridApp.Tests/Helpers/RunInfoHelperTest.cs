using McpKsef.HybridApp.Helpers;

public class RunInfoHelperTest
{
    private (InfoHelperResultVo result, string output) RunWithEnv(
        string ksefToken = null,
        string vatId = null,
        bool createCertFiles = false,
        string certFilePath = null,
        string keyFilePath = null,
        string privateKeyPassword = null)
    {
        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);

        var origKsefToken = Environment.GetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefToken);
        var origVatId = Environment.GetEnvironmentVariable(Shared.Consts.EnvironmentConsts.VatId);
        var origCertFile = Environment.GetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefCertificateFile);
        var origKeyFile = Environment.GetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile);
        var origPassword = Environment.GetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefPrivateKeyPassword);

        string createdCert = null;
        string createdKey = null;

        try
        {
            if (createCertFiles)
            {
                createdCert = Path.GetTempFileName();
                File.WriteAllText(createdCert, "dummy cert content");
                createdKey = Path.GetTempFileName();
                File.WriteAllText(createdKey, "dummy key content");
                certFilePath = createdCert;
                keyFilePath = createdKey;
            }

            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefToken, ksefToken);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.VatId, vatId);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefCertificateFile, certFilePath);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, keyFilePath);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefPrivateKeyPassword, privateKeyPassword);

            var result = McpKsef.HybridApp.Helpers.RunInfoHelper.CheckEnvironmentConsts();
            Console.Out.Flush();
            return (result, sw.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefToken, origKsefToken);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.VatId, origVatId);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefCertificateFile, origCertFile);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, origKeyFile);
            Environment.SetEnvironmentVariable(Shared.Consts.EnvironmentConsts.KsefPrivateKeyPassword, origPassword);

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
    public void TokenValid_CertFilesExistButPasswordMissing_TokenSavesConnection_ReturnsValid()
    {
        var (result, output) = RunWithEnv(
            ksefToken: "test-token",
            vatId: "1234567890",
            createCertFiles: true,
            privateKeyPassword: null);

        Assert.True(result.IsKsefTokenValid);
        Assert.False(result.IsKsefCertificateValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(result.IsValid);
        Assert.Contains("token KSeF", output);
        Assert.Contains("poprawnie", output);
        Assert.DoesNotContain(Shared.Consts.EnvironmentConsts.KsefPrivateKeyPassword, output);
    }

    [Fact]
    public void BothCertAndKeyFilesNonExistent_WritesBothFileNotFoundErrors()
    {
        var nonExistentCert = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".crt");
        var nonExistentKey = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".key");

        var (result, output) = RunWithEnv(
            certFilePath: nonExistentCert,
            keyFilePath: nonExistentKey,
            privateKeyPassword: "test-password");

        Assert.False(result.IsKsefCertificateValid);
        Assert.False(result.IsKsefTokenValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefCertificateFile, output);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, output);
        var certOccurrences = output.Split(Shared.Consts.EnvironmentConsts.KsefCertificateFile).Length - 1;
        var keyOccurrences = output.Split(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile).Length - 1;
        Assert.True(certOccurrences >= 1);
        Assert.True(keyOccurrences >= 1);
        Assert.Contains("nie został znaleziony", output);
    }

    [Fact]
    public void EmptyKeyFilePath_NonEmptyCertPath_WritesBothEmptyLocationErrors()
    {
        var nonExistentCert = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".crt");

        var (result, output) = RunWithEnv(
            certFilePath: nonExistentCert,
            keyFilePath: "",
            privateKeyPassword: "test-password");

        Assert.False(result.IsKsefCertificateValid);
        Assert.Contains("Nie podano lokalizacji pliku klucza prywatnego", output);
    }

    [Fact]
    public void TokenValid_NoCertEnvVarsAtAll_ReturnsValid_NoCertErrorMessages()
    {
        var (result, output) = RunWithEnv(
            ksefToken: "test-token",
            vatId: "1234567890");

        Assert.True(result.IsKsefTokenValid);
        Assert.False(result.IsKsefCertificateValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(Shared.Consts.EnvironmentConsts.KsefCertificateFile, output);
        Assert.DoesNotContain(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, output);
        Assert.DoesNotContain("nie został znaleziony", output);
    }

    [Fact]
    public void CertValidNoToken_VatValid_UsesCertForConnection_WritesCertificateInMessage()
    {
        var (result, output) = RunWithEnv(
            ksefToken: null,
            vatId: "1234567890",
            createCertFiles: true,
            privateKeyPassword: "test-password");

        Assert.False(result.IsKsefTokenValid);
        Assert.True(result.IsKsefCertificateValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(result.IsValid);
        Assert.Contains("certyfikat KSeF", output);
        Assert.DoesNotContain("token KSeF", output);
    }

    [Fact]
    public void NoEnvVarsAtAll_IsValidFalse_WritesAllThreeRequiredVarNames()
    {
        var (result, output) = RunWithEnv();

        Assert.False(result.IsValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefCertificateFile, output);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, output);
        Assert.Contains(Shared.Consts.EnvironmentConsts.VatId, output);
    }

    [Fact]
    public void CertFilesExistPasswordSet_NoToken_NoVat_CertValidButOverallInvalid()
    {
        var (result, output) = RunWithEnv(
            createCertFiles: true,
            privateKeyPassword: "secret",
            vatId: null);

        Assert.False(result.IsKsefTokenValid);
        Assert.True(result.IsKsefCertificateValid);
        Assert.False(result.IsVatIdValid);
        Assert.False(result.IsValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.VatId, output);
        Assert.DoesNotContain("Zmienne połączenia ustawione poprawnie", output);
    }
}