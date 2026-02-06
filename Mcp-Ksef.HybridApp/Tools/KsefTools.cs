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
    
    [McpServerTool(Name = "get_invoices_for_period", Title = "Pobierz faktury podanego okresu")]
    [Description("Pobiera listę faktur z podanego okresu z systemu ksef")]
    public async Task<PagedInvoiceResponse> GetInvoicesListForGivenDate(
        [Description("Data wystawienia faktury od")] DateTime dataFakturyOd,
        [Description("Data wystawienia faktury do")] DateTime dataFakturyDo)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoicesListForGivenDate)} called dataFakturyOd: {dataFakturyOd} dataFakturyDo {dataFakturyDo}");
        await VerifyAuthToken();

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
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoice_by_invoice_number", Title = "Pobierz fakturę o numerze faktury")]
    [Description("Pobiera fakturę wg numeru faktury")]
    public async Task<PagedInvoiceResponse> GetInvoiceByInvoiceNumber([Description("Numer faktury")] string invoiceNumber)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByInvoiceNumber)} called invoiceNumber: {invoiceNumber}");
        await VerifyAuthToken();
        
        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            InvoiceNumber = invoiceNumber,
            DateRange = GetMaxDataRange()
        };
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoices_for_buyer_by_nip", Title = "Pobierz faktury dla kupującego o numerze NIP")]
    [Description("Pobiera faktury dla kupującego o podanym NIP")]
    public async Task<PagedInvoiceResponse> GetInvoiceByBuyerNip([Description("Numer nip kupujacego")] string nip)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByBuyerNip)} called nip: {nip}");
        await VerifyAuthToken();

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
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authToken);
        return invoiceList;
    }
    
    [McpServerTool(Name = "get_invoices_for_buyer_by_vateu", Title = "Pobierz faktury dla kupującego o numerze VAT UE")]
    [Description("Pobiera faktury dla kupującego o podanym VAT UE")]
    public async Task<PagedInvoiceResponse> GetInvoiceByBuyerVatUe([Description("Numer vat eu kupujacego")] string vatUe)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByBuyerVatUe)} called nip: {vatUe}");
        await VerifyAuthToken();

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
        
        var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(invoiceMetadataQueryRequest, _authToken);
        return invoiceList;
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

