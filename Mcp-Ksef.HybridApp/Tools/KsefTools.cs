using System.ComponentModel;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using Shared.Consts;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public class KsefTools : IKsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string? _ksefToken;
    private readonly string? _vatId;
    private static string? _authToken;
    private readonly IAuthorizationClient _authorizationClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IKSeFClient _ksefClient;
    
    public KsefTools(ILogger<KsefTools> logger, IAuthorizationClient authorizationClient, ICryptographyService cryptographyService, IKSeFClient ksefClient)
    {
        _logger = logger;
        _ksefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
        _vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        _authorizationClient =  authorizationClient;
        _cryptographyService =  cryptographyService;
        _ksefClient = ksefClient;
    }
    
    [McpServerTool(Name = "get_invoice_by_ksef", Title = "Pobierz fakturę po numerze ksef")]
    [Description("Pobiera fakturę po numerze ksef")]
    public async Task<string> GetInvoice([Description("Numer ksef")] string ksefNumber)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoice)} called ksefNumber: {ksefNumber}");
        await VerifyAuthToken();
        
        var invoice = await _ksefClient.GetInvoiceAsync(ksefNumber, _authToken);
        
        return invoice;
    }
    
    private async Task<string> GetAccessTokenAsync(string nip, string ksefToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetAccessTokenAsync)} called nip: {nip}");
        
        const AuthenticationTokenContextIdentifierType contextType = AuthenticationTokenContextIdentifierType.Nip;
        AuthenticationTokenAuthorizationPolicy? authorizationPolicy = null;
        IAuthCoordinator authCoordinator = new AuthCoordinator(_authorizationClient);
            
        var result = await authCoordinator.AuthKsefTokenAsync(
            contextType,
            nip,
            ksefToken,
            _cryptographyService,
            EncryptionMethodEnum.Rsa,
            authorizationPolicy,
            CancellationToken.None
        );
        
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetAccessTokenAsync)} return AccessToken Length: {result.AccessToken.Token.Length}");
        
        return result.AccessToken.Token;
    }

    private async Task VerifyAuthToken()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            _authToken = await GetAccessTokenAsync(_vatId, _ksefToken); 
        }
    }
}

