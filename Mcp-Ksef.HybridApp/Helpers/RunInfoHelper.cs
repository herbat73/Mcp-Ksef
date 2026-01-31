using Shared.Consts;

namespace McpKsef.HybridApp.Helpers;

public static class RunInfoHelper
{
    public static bool IsSettingsValidToRun()
    {
        var ksefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
        var isKsefTokenValid = !string.IsNullOrEmpty(ksefToken);
        
        if (!isKsefTokenValid)
        {
            Console.WriteLine($"Environment setting {EnvironmentConsts.KsefToken} is not set. Add environment variable {EnvironmentConsts.KsefToken} with valid KSeF Token.");
        }
        var vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        var isVatIdValid = !string.IsNullOrEmpty(vatId);
        
        if (!isVatIdValid)
        {
            Console.WriteLine($"Environment setting {EnvironmentConsts.VatId} is not set. Add environment variable {EnvironmentConsts.VatId} with valid VatId.");
        }
        return isKsefTokenValid;
    }
}