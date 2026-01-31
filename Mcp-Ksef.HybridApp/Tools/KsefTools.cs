using System.ComponentModel;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using Shared.Consts;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public class KsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string? _ksefToken;
    private readonly string? _vatId;
    private static string? _authToken;
    private readonly IAuthorizationClient _authorizationClient;
    private readonly ICryptographyService _cryptographyService;
    
    //public KsefTools(KsefAppSettings ksefAppSettings, ILogger<KsefTools> logger)
    public KsefTools(ILogger<KsefTools> logger, IAuthorizationClient authorizationClient, ICryptographyService cryptographyService)
    {
        _logger = logger;
        _ksefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
        _vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        _authorizationClient =  authorizationClient;
        _cryptographyService =  cryptographyService;
    }
    
    [McpServerTool(Name = "get_invoice_by_reference", Title = "Pobierz fakturę po numerze referencyjnym")]
    [Description("Pobiera fakturę po numerze referencyjnym")]
    public async Task<string> GetInvoice([Description("Numer referencyjny ksef")] string ksefReferenceNumber)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoice)} called invoiceNumber: {ksefReferenceNumber}");
        await VerifyAuthToken();
        
        var invoice = $"yo {ksefReferenceNumber} ksefToken {_ksefToken}";
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

