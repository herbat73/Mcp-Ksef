using System;
using System.ComponentModel;
using McpKsef.HybridApp.Configurations;
using Shared.Consts;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public class KsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string? _ksefToken;
    private readonly IConfiguration _configuration;
    //private readonly KsefAppSettings _ksefAppSettings;
    
    //public KsefTools(KsefAppSettings ksefAppSettings, ILogger<KsefTools> logger)
    public KsefTools(IConfiguration configuration, ILogger<KsefTools> logger)
    {
      //  _ksefAppSettings =  ksefAppSettings;
        _configuration = configuration;
        _logger = logger;
        _ksefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);

        var connectionSettings = _configuration.GetSection("Connection");
        
        //_logger.LogInformation($"UseProduction {ksefAppSettings.Connection.UseProduction}");
        
        if (string.IsNullOrEmpty(_ksefToken))
        {
            _logger.LogError($"{AppConsts.KsefToolName}.{nameof(KsefTools)} environment variable {EnvironmentConsts.KsefToken} not set");
        }
    }
    
    [McpServerTool(Name = "get_invoice_by_reference", Title = "Pobierz fakturę po numerze referencyjnym")]
    [Description("Pobiera fakturę po numerze referencyjnym")]
    public string GetInvoice([Description("Numer referencyjny ksef")] string ksefReferenceNumber)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoice)} called invoiceNumber: {ksefReferenceNumber}");
        
        var invoice = $"yo {ksefReferenceNumber} ksefToken {_ksefToken}";
        return invoice;
    }
}

