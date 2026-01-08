using System.ComponentModel;
using KSeF.Client.Api.Services;
using KSeF.Client.Clients;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using ModelContextProtocol.Server;
using RemoteMcpKsef.Models;

namespace RemoteMcpKsef.Tools;

/// <summary>
/// Data manipulation tools for the MCP server.
/// Provides text processing and data transformation capabilities.
/// </summary>
[McpServerToolType]
public class KsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string _toolName = "KSeF Tools";
    private IKSeFClient _ksefClient;
    private readonly IAuthorizationClient _authorizationClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IConfiguration _configuration;
    private string? _accessToken = null;
    private string? _openSessionReferenceNumber =  null;
    
    public KsefTools(ILogger<KsefTools> logger, IKSeFClient ksefClient, IAuthorizationClient authorizationClient, ICryptographyService cryptographyService, IConfiguration configuration)
    {
        _logger = logger;
        _ksefClient =  ksefClient;
        _authorizationClient = authorizationClient;
        _cryptographyService = cryptographyService;
        _configuration = configuration;
    }
    
    [McpServerTool, Description("Pobranie faktury po numerze referencyjnym")]
    public async Task<string> GetInvoice([Description("Numer referencyjny ksef")] string ksefReferenceNumber)
    {
        _logger.LogInformation($"{_toolName}.{nameof(GetInvoice)} called invoiceNumber: {ksefReferenceNumber}");
        var sellerSettings = _configuration.GetSection("SellerSettings").Get<CompanyInfo>();
        var ksefSettings = _configuration.GetSection(nameof(KsefSettings)).Get<KsefSettings>();

        _accessToken ??= await GetAccessTokenAsync(sellerSettings.VatId, ksefSettings.Token);
        
        var invoice = await _ksefClient.GetInvoiceAsync(ksefReferenceNumber, _accessToken);
        return invoice;
    }
    
    private async Task<string> GetAccessTokenAsync(string nip, string ksefToken)
    {
        _logger.LogInformation($"GetAccessTokenAsync for nip : {nip}");
        
        const AuthenticationTokenContextIdentifierType contextType = AuthenticationTokenContextIdentifierType.Nip;
        AuthenticationTokenAuthorizationPolicy? authorizationPolicy = null;
        var authCoordinator = new AuthCoordinator(_authorizationClient);
            
        var result = await authCoordinator.AuthKsefTokenAsync(
            contextType,
            nip,
            ksefToken,
            _cryptographyService,
            EncryptionMethodEnum.Rsa,
            authorizationPolicy,
            CancellationToken.None
        );
            
        return result.AccessToken.Token;
    }
}