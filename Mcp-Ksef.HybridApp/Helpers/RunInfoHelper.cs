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
        return isKsefTokenValid;
    }
}