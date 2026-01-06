using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// General utility tools for the MCP server.
/// Provides system utilities and helper functions.
/// </summary>
[McpServerToolType] 
public static class UtilityTools
{
    [McpServerTool, Description("Echoes the input message back to the client")]
    public static string Echo([Description("The message to echo")] string message)
    {
        return $"Echo: {message}";
    }

    [McpServerTool, Description("Gets the current server time")]
    public static string GetCurrentTime()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    }

    [McpServerTool, Description("Generates a random number between min and max")]
    public static int GenerateRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 1,
        [Description("Maximum value (inclusive)")] int max = 100)
    {
        var random = new Random();
        return random.Next(min, max + 1);
    }
}