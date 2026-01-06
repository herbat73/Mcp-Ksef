using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

/// <summary>
/// Data manipulation tools for the MCP server.
/// Provides text processing and data transformation capabilities.
/// </summary>
[McpServerToolType]
public class KsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string _toolName = "KSeF Tools";
    
    public KsefTools(ILogger<KsefTools> logger)
    {
        _logger = logger;
    }
    
    [McpServerTool, Description("Pobierz fakturę")]
    public string GetInvoice([Description("Numer faktury")] string invoiceNumber)
    {
        _logger.LogInformation($"{_toolName}.{nameof(GetInvoice)} called invoiceNumber: {invoiceNumber}");
        
        return new string(invoiceNumber.Reverse().ToArray());
    }
}