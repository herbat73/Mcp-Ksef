using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Api.Builders.Auth;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using McpKsef.HybridApp.Helpers;
using Shared.Consts;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public class KsefTools : IKsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string? _vatId;
    //private static string? _authToken;
    private static AuthenticationOperationStatusResponse? _authenticationResponse;
    private readonly IAuthorizationClient _authorizationClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IKSeFClient _ksefClient;
    private readonly IVerificationLinkService _verificationLinkService;
    
    public KsefTools(ILogger<KsefTools> logger, IAuthorizationClient authorizationClient, ICryptographyService cryptographyService, IKSeFClient ksefClient, IVerificationLinkService  verificationLinkService)
    {
        _logger = logger;
        _vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        _authorizationClient =  authorizationClient;
        _cryptographyService =  cryptographyService;
        _ksefClient = ksefClient;
        _verificationLinkService =  verificationLinkService;
    }
    
    [McpServerTool(Name = "get_invoice_by_ksef", Title = "Pobierz fakturę po numerze ksef")]
    [Description("Pobiera fakturę po numerze ksef")]
    public async Task<string> GetInvoice([Description("Numer ksef")] string ksefNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoice)} called ksefNumber: {ksefNumber}");
        await VerifyAuthToken(cancellationToken);
        
        var invoice = await _ksefClient.GetInvoiceAsync(ksefNumber, _authenticationResponse?.AccessToken.Token, cancellationToken);
        
        return invoice;
    }
    
    [McpServerTool(Name = "get_invoices_for_period", Title = "Pobierz faktury podanego okresu")]
    [Description("Pobiera listę faktur z podanego okresu z systemu ksef")]
    public async Task<PagedInvoiceResponse> GetInvoicesListForGivenDate(
        [Description("Data wystawienia faktury od")] DateTime dataFakturyOd,
        [Description("Data wystawienia faktury do")] DateTime dataFakturyDo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoicesListForGivenDate)} called dataFakturyOd: {dataFakturyOd} dataFakturyDo {dataFakturyDo}");
        await VerifyAuthToken(cancellationToken);

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = InvoiceSubjectType.Subject1,
            DateRange = new DateRange
            {
                From = dataFakturyOd,
                To = dataFakturyDo,
                DateType = DateType.Issue
            }
        };
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authenticationResponse?.AccessToken.Token, cancellationToken: cancellationToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoice_by_invoice_number", Title = "Pobierz fakturę o numerze faktury")]
    [Description("Pobiera fakturę wg numeru faktury")]
    public async Task<PagedInvoiceResponse> GetInvoiceByInvoiceNumber([Description("Numer faktury")] string invoiceNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByInvoiceNumber)} called invoiceNumber: {invoiceNumber}");
        await VerifyAuthToken(cancellationToken);
        
        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            InvoiceNumber = invoiceNumber,
            DateRange = GetMaxDataRange()
        };
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authenticationResponse?.AccessToken.Token, cancellationToken: cancellationToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoices_for_buyer_by_nip", Title = "Pobierz faktury dla kupującego o numerze NIP")]
    [Description("Pobiera faktury dla kupującego o podanym NIP")]
    public async Task<PagedInvoiceResponse> GetInvoiceByBuyerNip([Description("Numer nip kupujacego")] string nip, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByBuyerNip)} called nip: {nip}");
        await VerifyAuthToken(cancellationToken);

        var buyerIdentifier = new BuyerIdentifier
        {
            Type = BuyerIdentifierType.Nip,
            Value = nip
        };

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            BuyerIdentifier = buyerIdentifier,
            DateRange = GetMaxDataRange()
        };
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authenticationResponse?.AccessToken.Token, cancellationToken: cancellationToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoices_for_buyer_by_vateu", Title = "Pobierz faktury dla kupującego o numerze VAT UE")]
    [Description("Pobiera faktury dla kupującego o podanym VAT UE")]
    public async Task<PagedInvoiceResponse> GetInvoiceByBuyerVatUe([Description("Numer vat eu kupujacego")] string vatUe, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByBuyerVatUe)} called nip: {vatUe}");
        await VerifyAuthToken(cancellationToken);

        var buyerIdentifier = new BuyerIdentifier
        {
            Type = BuyerIdentifierType.VatUe,
            Value = vatUe
        };

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            BuyerIdentifier = buyerIdentifier,
            DateRange = GetMaxDataRange()
        };
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authenticationResponse?.AccessToken.Token, cancellationToken: cancellationToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoice_url_by_ksef", Title = "Pobierz link do faktury po numerze ksef")]
    [Description("Zwraca link do faktury po numerze ksef")]
    public async Task<string> GetInvoiceUrl([Description("Numer ksef")] string ksefNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceUrl)} called ksefNumber: {ksefNumber}");
        await VerifyAuthToken(cancellationToken);
        
        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            KsefNumber = ksefNumber,
            DateRange = GetMaxDataRange()
        };
        
        var metadata = await _ksefClient.QueryInvoiceMetadataAsync(
                    requestPayload: invoiceMetadataQueryRequest,
                    accessToken: _authenticationResponse?.AccessToken.Token,
                    cancellationToken: cancellationToken);
        
        var invoiceMetadata = metadata.Invoices.Single(x => x.KsefNumber == ksefNumber);
        var invoiceHash = invoiceMetadata.InvoiceHash;
        var invoicingDate = invoiceMetadata.InvoicingDate;
        
        var invoiceForOnlineUrl = _verificationLinkService.BuildInvoiceVerificationUrl(_vatId, invoicingDate.DateTime, invoiceHash);
        
        return invoiceForOnlineUrl;
    }

    private static DateRange GetMaxDataRange()
    {
        return new DateRange
        {
            From = DateTime.UtcNow.AddMonths(-3).AddMinutes(1),
            To = DateTime.UtcNow,
            DateType = DateType.Issue
        };
    }
    
    private async Task<AuthenticationOperationStatusResponse> GetAccessTokenFromKsefTokenAsync(string nip, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetAccessTokenFromKsefTokenAsync)} called nip: {nip}");
        
        const AuthenticationTokenContextIdentifierType contextType = AuthenticationTokenContextIdentifierType.Nip;
        AuthenticationTokenAuthorizationPolicy? authorizationPolicy = null;
        var authCoordinator = new AuthCoordinator(_authorizationClient);
            
        var accessTokenResponse = await authCoordinator.AuthKsefTokenAsync(
            contextType,
            nip,
            Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken),
            _cryptographyService,
            EncryptionMethodEnum.Rsa,
            authorizationPolicy,
            cancellationToken
        );
        
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetAccessTokenFromKsefTokenAsync)} return AccessToken Length: {accessTokenResponse.AccessToken.Token.Length} valid until {accessTokenResponse.AccessToken.ValidUntil}");
        
        return accessTokenResponse;
    }
    
    private async Task<AuthenticationOperationStatusResponse> GetAccessTokenByCertAsync(string nip, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetAccessTokenByCertAsync)} called nip: {nip}");
        
        var ksefCertificateFile = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile);
        var ksefPrivateKeyFile = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile);
        var ksefPrivateKeyPassword = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword);
        
        var certContent = await File.ReadAllTextAsync(ksefCertificateFile, cancellationToken);
        var privateKeyContent = await File.ReadAllTextAsync(ksefPrivateKeyFile, cancellationToken);
        var certificate = X509Certificate2.CreateFromEncryptedPem(certContent, privateKeyContent, ksefPrivateKeyPassword);
        
        var challengeResponse = await _authorizationClient.GetAuthChallengeAsync(cancellationToken);
        var challenge = challengeResponse.Challenge;
        
        const AuthenticationTokenContextIdentifierType contextType = AuthenticationTokenContextIdentifierType.Nip;
        
        var authTokenRequest =
            AuthTokenRequestBuilder
                .Create()
                .WithChallenge(challenge)
                .WithContext(contextType, nip)
                .WithIdentifierType(AuthenticationTokenSubjectIdentifierTypeEnum.CertificateSubject);

        var authorizeRequest = authTokenRequest.Build();
        
        var unsignedXml = AuthenticationTokenRequestSerializer.SerializeToXmlString(authorizeRequest);
        var signedXml = SignatureService.Sign(unsignedXml, certificate);

        var authSubmission = await _authorizationClient
            .SubmitXadesAuthRequestAsync(signedXml, false, cancellationToken);

        AuthStatus authStatus;
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMinutes(2);

        do
        {
            authStatus = await _authorizationClient.GetAuthStatusAsync(authSubmission.ReferenceNumber, authSubmission.AuthenticationToken.Token, cancellationToken);
            if (authStatus.Status.Code != 200)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        while (authStatus.Status.Code != 200 && (DateTime.UtcNow - startTime) < timeout);
        
        if (authStatus.Status.Code != 200)
        {
            throw new TimeoutException("Timeout Uwierzytelniania: Brak tokena po 2 minutach.");
        }

        var accessTokenResponse =
            await _authorizationClient.GetAccessTokenAsync(authSubmission.AuthenticationToken.Token, cancellationToken);
            
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetAccessTokenByCertAsync)} return AccessToken Length: {accessTokenResponse.AccessToken.Token.Length} valid until {accessTokenResponse.AccessToken.ValidUntil}");
        
        return accessTokenResponse;
    }

    private async Task VerifyAuthToken(CancellationToken cancellationToken)
    {
        if (_authenticationResponse == null)
        {
            var infoHelperResult = RunInfoHelper.CheckEnvironmentConsts();
            _authenticationResponse = infoHelperResult.IsKsefCertificateValid ? 
                await GetAccessTokenByCertAsync(_vatId, cancellationToken) : 
                await GetAccessTokenFromKsefTokenAsync(_vatId, cancellationToken); 
        }
        else
        {
            var refreshTime = DateTime.Now.AddMinutes(5);
            if (_authenticationResponse?.AccessToken.ValidUntil < refreshTime)
            {
                _logger.LogInformation($"Refreshing token as it is valid until: {_authenticationResponse?.AccessToken.ValidUntil}");
                
                var tokenRefreshResponse =
                    await _ksefClient.RefreshAccessTokenAsync(_authenticationResponse!.RefreshToken.Token, cancellationToken);
                _authenticationResponse.AccessToken = tokenRefreshResponse.AccessToken;
                _logger.LogInformation($"Token refreshed, now it valid until: {_authenticationResponse?.AccessToken.ValidUntil}");
            }
        }
    }
}

