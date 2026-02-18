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
    public void TokenAndVatSet_ReturnsValid_WritesSuccessMessageWithToken()
    {
        var (result, output) = RunWithEnv(ksefToken: "test-token", vatId: "1234567890");

        Assert.True(result.IsKsefTokenValid);
        Assert.True(result.IsVatIdValid);
        Assert.False(result.IsKsefCertificateValid);
        Assert.True(result.IsValid);
        Assert.Contains("token KSeF", output);
        Assert.Contains("poprawnie", output);
    }

    [Fact]
    public void MissingVat_ReturnsInvalid_WritesVatMessage()
    {
        var (result, output) = RunWithEnv(ksefToken: "test-token", vatId: null);

        Assert.True(result.IsKsefTokenValid);
        Assert.False(result.IsVatIdValid);
        Assert.False(result.IsValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.VatId, output);
    }

    [Fact]
    public void NoAuthMethod_ReturnsAllInvalid_WritesMultipleErrors()
    {
        var (result, output) = RunWithEnv();

        Assert.False(result.IsKsefTokenValid);
        Assert.False(result.IsKsefCertificateValid);
        Assert.False(result.IsVatIdValid);
        Assert.False(result.IsValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefCertificateFile, output);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, output);
        Assert.Contains(Shared.Consts.EnvironmentConsts.VatId, output);
    }

    [Fact]
    public void ValidCertificateNoToken_WritesAboutMissingTokenOrCert()
    {
        var (result, output) = RunWithEnv(
            createCertFiles: true,
            privateKeyPassword: "test-password",
            vatId: "1234567890");

        Assert.False(result.IsKsefTokenValid);
        Assert.True(result.IsKsefCertificateValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(result.IsValid);
        Assert.Contains("certyfikat KSeF", output);
    }

    [Fact]
    public void CertificateFileMissing_CertInvalid_WritesFileNotFound()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".crt");
        
        var (result, output) = RunWithEnv(
            certFilePath: nonExistentPath,
            keyFilePath: Path.GetTempFileName(),
            privateKeyPassword: "test-password");

        Assert.False(result.IsKsefCertificateValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefCertificateFile, output);
        Assert.Contains("nie został znaleziony", output);
    }

    [Fact]
    public void PrivateKeyFileMissing_CertInvalid_WritesKeyFileNotFound()
    {
        var tempCert = Path.GetTempFileName();
        File.WriteAllText(tempCert, "cert");
        var nonExistentKeyPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".key");

        try
        {
            var (result, output) = RunWithEnv(
                certFilePath: tempCert,
                keyFilePath: nonExistentKeyPath,
                privateKeyPassword: "test-password");

            Assert.False(result.IsKsefCertificateValid);
            Assert.Contains(Shared.Consts.EnvironmentConsts.KsefPrivateKeyFile, output);
            Assert.Contains("nie został znaleziony", output);
        }
        finally
        {
            if (File.Exists(tempCert))
                File.Delete(tempCert);
        }
    }

    [Fact]
    public void PasswordMissing_FilesExist_CertInvalid_WritesPasswordMissing()
    {
        var (result, output) = RunWithEnv(
            createCertFiles: true,
            privateKeyPassword: null);

        Assert.False(result.IsKsefCertificateValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.KsefPrivateKeyPassword, output);
        Assert.Contains("hasła", output);
    }

    [Fact]
    public void AllParametersValidWithCertificate_ReturnsValid_WritesCertificateSuccess()
    {
        var (result, output) = RunWithEnv(
            ksefToken: "test-token",
            vatId: "1234567890",
            createCertFiles: true,
            privateKeyPassword: "test-password");

        Assert.True(result.IsKsefTokenValid);
        Assert.True(result.IsKsefCertificateValid);
        Assert.True(result.IsVatIdValid);
        Assert.True(result.IsValid);
        Assert.Contains("certyfikat KSeF", output);
        Assert.Contains("poprawnie", output);
    }

    [Fact]
    public void OnlyCertificateValidNoVat_ReturnsInvalid_WritesVatError()
    {
        var (result, output) = RunWithEnv(
            createCertFiles: true,
            privateKeyPassword: "test-password",
            vatId: null);

        Assert.False(result.IsKsefTokenValid);
        Assert.True(result.IsKsefCertificateValid);
        Assert.False(result.IsVatIdValid);
        Assert.False(result.IsValid);
        Assert.Contains(Shared.Consts.EnvironmentConsts.VatId, output);
    }

    [Fact]
    public void EmptyCertificateFilePath_CertInvalid_WritesNoPodano()
    {
        var (result, output) = RunWithEnv(
            certFilePath: "",
            keyFilePath: "",
            privateKeyPassword: "test-password");

        Assert.False(result.IsKsefCertificateValid);
        Assert.Contains("Nie podano lokalizacji pliku certyfikatu", output);
        Assert.Contains("Nie podano lokalizacji pliku klucza prywatnego", output);
    }
}