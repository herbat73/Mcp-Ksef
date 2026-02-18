using Shared.Consts;

namespace McpKsef.HybridApp.Helpers;

public static class RunInfoHelper
{
    public static InfoHelperResultVo CheckEnvironmentConsts()
    {
        var result = new InfoHelperResultVo();
        
        var ksefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
        result.IsKsefTokenValid = !string.IsNullOrEmpty(ksefToken);
        var ksefCertificateFile = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile);
        var ksefCertificateFileExists = !string.IsNullOrEmpty(ksefCertificateFile) && File.Exists(ksefCertificateFile);
        var ksefPrivateKeyFile = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile);
        var ksefPrivateKeyFileExists = !string.IsNullOrEmpty(ksefPrivateKeyFile) && File.Exists(ksefPrivateKeyFile);
        var ksefPrivateKeyPassword = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword);
        
        result.IsKsefCertificateValid = ksefCertificateFileExists &&
                 ksefPrivateKeyFileExists &&
                 !string.IsNullOrEmpty(ksefPrivateKeyPassword);
        
        if (result is { IsKsefCertificateValid: false, IsKsefTokenValid: false })
        {
            Console.WriteLine($"Zmienne środowiskowe niezbędne do połączenia z KSeF nie są ustawione poprawnie.");
            if (string.IsNullOrEmpty(ksefCertificateFile))
            {
                Console.WriteLine($"Nie podano lokalizacji pliku certyfikatu w zmiennej {EnvironmentConsts.KsefCertificateFile}");
            }
            if (!string.IsNullOrEmpty(ksefCertificateFile) && !File.Exists(ksefCertificateFile))
            {
                Console.WriteLine($"Podany plik w {EnvironmentConsts.KsefCertificateFile} nie został znaleziony w podanej lokalizacji: {ksefCertificateFile}");
            }
            if (string.IsNullOrEmpty(ksefPrivateKeyFile))
            {
                Console.WriteLine($"Nie podano lokalizacji pliku klucza prywatnego w zmiennej {EnvironmentConsts.KsefPrivateKeyFile}");
            }
            if (!string.IsNullOrEmpty(ksefPrivateKeyFile) && !File.Exists(ksefPrivateKeyFile))
            {
                Console.WriteLine($"Podany plik w {EnvironmentConsts.KsefPrivateKeyFile} nie został znaleziony w podanej lokalizacji: {ksefPrivateKeyFile}");
            }
            if (ksefCertificateFileExists && ksefPrivateKeyFileExists && string.IsNullOrEmpty(ksefPrivateKeyPassword))
            {
                Console.WriteLine($"Nie podano hasła do klucza prywatnego w parametrze {EnvironmentConsts.KsefPrivateKeyPassword}");
            }
            if (result is { IsKsefCertificateValid: true, IsKsefTokenValid: false })
            {
                Console.WriteLine($"Nie podano poprawnych parametrów dla certyfikatu ani dla tokenu KSeF {EnvironmentConsts.KsefToken}");
            }
        }
        
        var vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        result.IsVatIdValid = !string.IsNullOrEmpty(vatId);
        
        if (!result.IsVatIdValid)
        {
            Console.WriteLine($"Zmienna środowiskowa {EnvironmentConsts.VatId} nie została ustawiona. Dodaj {EnvironmentConsts.VatId} z poprawnym numerem NIP (bez znaków formatujących (same numery)");
        }

        if (result.IsValid)
        {
            var whatToUseForConnection = result.IsKsefCertificateValid ? "certyfikat KSeF" : "token KSeF";
            Console.WriteLine($"Zmienne połączenia ustawione poprawnie. Zostanie użyty {whatToUseForConnection} do nawiązania połączenia");
        }
        
        return result;
    }
}