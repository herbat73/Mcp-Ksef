using System;
using System.ComponentModel;
using McpKsef.HybridApp.Consts;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public class KsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private  readonly string? _ksefToken;
    
    public KsefTools(ILogger<KsefTools> logger)
    {
        _logger = logger;
        _ksefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);

        if (string.IsNullOrEmpty(_ksefToken))
        {
            _logger.LogError($"{AppConsts.KsefToolName}.{nameof(KsefTools)} environment variable {EnvironmentConsts.KsefToken} not set");
        }
    }

    [McpServerTool, Description("Pobranie faktury po numerze referencyjnym")]
    public string GetInvoice([Description("Numer referencyjny ksef")] string ksefReferenceNumber)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoice)} called invoiceNumber: {ksefReferenceNumber}");
        
        var invoice = $"yo {ksefReferenceNumber} ksefToken {_ksefToken}";
        return invoice;
    }
}