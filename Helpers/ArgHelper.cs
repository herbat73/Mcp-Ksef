using Services;

namespace RemoteMcpKsef.Helpers;

public static class ArgHelper
{
    public static async Task Resolve(string[] args)
    {
        var command = args[0].ToLowerInvariant();
        var serviceManager = new ServiceManager();
        
        switch (command)
        {
            case "--daemon":
            case "daemon":
                Console.WriteLine("Starting Remote MCP Server as daemon...");
                break;
            
            case "--status":
            case "status":
                await serviceManager.ShowStatusAsync();
                return;
            
            case "--stop":
            case "stop":
                await serviceManager.StopServiceAsync();
                return;
            
            case "--install-service":
            case "install-service":
                await serviceManager.InstallServiceAsync();
                return;
            
            case "--uninstall-service":
            case "uninstall-service":
                await serviceManager.UninstallServiceAsync();
                return;
            
            case "--help":
            case "help":
            case "-h":
                InfoHelper.ShowHelp();
                return;
            
            default:
                if (!command.StartsWith("--"))
                {
                    Console.WriteLine($"Unknown command: {command}");
                    InfoHelper.ShowHelp();
                    return;
                }
                break;
        }
    }
}