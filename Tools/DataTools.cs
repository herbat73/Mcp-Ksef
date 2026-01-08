using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace RemoteMcpKsef.Tools;

/// <summary>
/// Data manipulation tools for the MCP server.
/// Provides text processing and data transformation capabilities.
/// </summary>
[McpServerToolType]
public static class DataTools
{
    [McpServerTool, Description("Converts a JSON string to formatted JSON")]
    public static string FormatJson([Description("The JSON string to format")] string jsonString)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
            return JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }
        catch (JsonException ex)
        {
            return $"Invalid JSON: {ex.Message}";
        }
    }

    [McpServerTool, Description("Converts text to uppercase")]
    public static string ToUpperCase([Description("The text to convert")] string text)
    {
        return text.ToUpperInvariant();
    }

    [McpServerTool, Description("Converts text to lowercase")]
    public static string ToLowerCase([Description("The text to convert")] string text)
    {
        return text.ToLowerInvariant();
    }

    [McpServerTool, Description("Reverses the input text")]
    public static string ReverseText([Description("The text to reverse")] string text)
    {
        return new string(text.Reverse().ToArray());
    }
}