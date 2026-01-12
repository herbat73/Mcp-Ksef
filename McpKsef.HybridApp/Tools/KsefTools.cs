using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public class KsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string _toolName = "KSeF Tools";
    
    public KsefTools(ILogger<KsefTools> logger)
    {
        _logger = logger;
    }

    [McpServerTool, Description("Pobranie faktury po numerze referencyjnym")]
    public string GetInvoice([Description("Numer referencyjny ksef")] string ksefReferenceNumber)
    {
        _logger.LogInformation($"{_toolName}.{nameof(GetInvoice)} called invoiceNumber: {ksefReferenceNumber}");

        var ksefToken = Environment.GetEnvironmentVariable("KSEF_TOKEN");
        var invoice = $"yo {ksefReferenceNumber} ksefToken {ksefToken}";
        return invoice;
    }
}