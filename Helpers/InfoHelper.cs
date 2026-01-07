namespace RemoteMcpKsef.Helpers;

public static class InfoHelper
{
    public static void ShowHelp()
    {
        Console.WriteLine("Remote MCP KSeF Server - Cross-platform service hosting");
        Console.WriteLine();
        Console.WriteLine("Usage: mcp-ksef [command]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  daemon             Run as background daemon/service");
        Console.WriteLine("  status             Show service status");
        Console.WriteLine("  stop               Stop running service");
        Console.WriteLine("  install-service    Install as system service");
        Console.WriteLine("  uninstall-service  Remove system service");
        Console.WriteLine("  help               Show this help");
        Console.WriteLine();
        Console.WriteLine("Default (no command): Run as interactive server");
    }
}