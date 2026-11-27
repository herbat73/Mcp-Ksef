using System.ComponentModel;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Invoices;
using McpKsef.HybridApp.Services;
using ModelContextProtocol.Protocol;
using Shared.Consts;
using ModelContextProtocol.Server;

namespace McpKsef.HybridApp.Tools;

[McpServerToolType]
public partial class KsefTools : IKsefTools
{
    private readonly ILogger<KsefTools> _logger;
    private readonly string? _vatId;
    private readonly IKSeFClient _ksefClient;
    private readonly IVerificationLinkService _verificationLinkService;
    private readonly IKsefAuthorizationService _ksefAuthorization;
    private const string SearchResultDescription = "Zwraca metadane faktur (m.in. numer KSeF, numer faktury, daty, kwoty, dane stron) bez pełnej treści – do jej pobrania użyj get_invoice_by_ksef. Wyniki są stronicowane: domyślnie zwracana jest pierwsza strona (pageOffset = 0) po 10 faktur. Gdy HasMore = true, pobierz kolejną stronę, zwiększając pageOffset o 1. IsTruncated = true oznacza, że KSeF obciął wynik – zawęź zakres dat. Gdy zapytanie się nie powiedzie, Error = true, a ErrorMessage zawiera opis błędu.";
    private const string SearchScopeDescription = "Domyślnie przeszukuje faktury wystawione przez nasz podmiot (sprzedażowe); parametrem invoiceSubjectType można przeszukać np. faktury zakupowe. Obejmuje faktury z ostatnich 3 miesięcy; rodzaj daty określa parametr dateType.";
    private const string InvoiceSubjectTypeDescription = "Rola naszego podmiotu na fakturze: Subject1 – sprzedawca, czyli faktury wystawione/sprzedażowe (domyślnie), Subject2 – nabywca, czyli faktury otrzymane/zakupowe, Subject3 – podmiot trzeci (np. odbiorca, płatnik, faktor), SubjectAuthorized – podmiot upoważniony";
    private const string KsefNumberDescription = "Numer KSeF faktury nadany przez system KSeF, np. 5265877635-20250826-0100001AF629-AF";
    private const string DateTypeDescription = "Typ daty, według której filtrowane są faktury: Issue – data wystawienia faktury (domyślnie), Invoicing – data przyjęcia faktury w KSeF (nadania numeru KSeF), PermanentStorage – data trwałego zapisu faktury w repozytorium KSeF";
    private const int DefaultPageSize = 10;
    private const int MinPageSize = 10;
    private const int MaxPageSize = 250;
    private const string PageOffsetDescription = "Numer strony wyników, liczony od 0 (domyślnie 0). Użyj kolejnych wartości, gdy w poprzednim wyniku HasMore = true.";
    private const string PageSizeDescription = "Liczba faktur na stronie, od 10 do 250 (domyślnie 10).";
    private const string KsefError = "Wystąpił nieoczekiwany błąd podczas wykonania zapytania do KSeF - ";
    
    public KsefTools(ILogger<KsefTools> logger, IKsefAuthorizationService ksefAuthorization, IKSeFClient ksefClient, IVerificationLinkService verificationLinkService)
    {
        _logger = logger;
        _vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        _ksefAuthorization =  ksefAuthorization;
        _ksefClient = ksefClient;
        _verificationLinkService =  verificationLinkService;
    }
    
    [McpServerTool(Name = "get_invoice_by_ksef", Title = "Pobierz fakturę po numerze ksef")]
    [Description("Pobiera pełną treść faktury z KSeF na podstawie numeru KSeF i zwraca ją jako dokument XML (struktura logiczna FA). Użyj, gdy potrzebujesz szczegółów faktury: pozycji, stawek i kwot VAT, danych sprzedawcy i nabywcy. Numer KSeF można znaleźć narzędziami wyszukującymi faktury (pole KsefNumber w wynikach).")]
    public async Task<string> GetInvoice([Description(KsefNumberDescription)] string ksefNumber, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoice)} called ksefNumber: {ksefNumber}");
        await _ksefAuthorization.VerifyAuthToken(cancellationToken);
        
        var invoice = await _ksefClient.GetInvoiceAsync(ksefNumber, _ksefAuthorization.GetAuthenticationInfo().AccessToken.Token, cancellationToken);
        
        return invoice;
    }
    
    [McpServerTool(Name = "query_invoices", Title = "Pobierz faktury na podstawie zapytania")]
    [Description("Wyszukuje w KSeF faktury z podanego zakresu dat (maksymalnie 3 miesiące). Domyślnie zwraca faktury wystawione przez nasz podmiot (sprzedażowe), filtrowane po dacie wystawienia. Parametrem invoiceSubjectType można pobrać np. faktury zakupowe, a parametrem dateType zmienić rodzaj daty. " + SearchResultDescription)]
    public async Task<McpPagedInvoiceResponse> QueryInvoices(
        [Description("Początek zakresu dat (włącznie). Rodzaj daty (wystawienia, przyjęcia w KSeF lub trwałego zapisu) określa parametr dateType. Zakres od-do może obejmować maksymalnie 3 miesiące")] DateTime dataFakturyOd,
        [Description("Koniec zakresu dat (włącznie). Rodzaj daty określa parametr dateType")] DateTime dataFakturyDo,
        CancellationToken cancellationToken,
        [Description(InvoiceSubjectTypeDescription)] InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1,
        [Description(DateTypeDescription)] DateType dateType = DateType.Issue,
        [Description(PageOffsetDescription)] int pageOffset = 0,
        [Description(PageSizeDescription)] int pageSize = DefaultPageSize)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(QueryInvoices)} called dataFakturyOd: {dataFakturyOd} dataFakturyDo {dataFakturyDo} invoiceSubjectType {invoiceSubjectType} dateType {dateType} pageOffset {pageOffset} pageSize {pageSize}");

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = invoiceSubjectType,
            DateRange = new DateRange
            {
                From = dataFakturyOd,
                To = dataFakturyDo,
                DateType = dateType
            }
        };

        return await QueryInvoicePage(invoiceMetadataQueryRequest, pageOffset, pageSize, cancellationToken);
    }

    [McpServerTool(Name = "get_invoice_by_invoice_number", Title = "Pobierz fakturę o numerze faktury")]
    [Description("Wyszukuje fakturę po numerze nadanym przez wystawcę (np. FV/2026/01/001). " + SearchScopeDescription + " " + SearchResultDescription)]
    public async Task<McpPagedInvoiceResponse> GetInvoiceByInvoiceNumber(
        [Description("Numer faktury nadany przez wystawcę (pole P_2 faktury), np. FV/2026/01/001")] string invoiceNumber,
        CancellationToken cancellationToken,
        [Description(InvoiceSubjectTypeDescription)] InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1,
        [Description(DateTypeDescription)] DateType dateType = DateType.Issue,
        [Description(PageOffsetDescription)] int pageOffset = 0,
        [Description(PageSizeDescription)] int pageSize = DefaultPageSize)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByInvoiceNumber)} called invoiceNumber: {invoiceNumber} invoiceSubjectType: {invoiceSubjectType} dateType: {dateType} pageOffset: {pageOffset} pageSize: {pageSize}");

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = invoiceSubjectType,
            InvoiceNumber = invoiceNumber,
            DateRange = GetMaxDataRange(dateType)
        };

        return await QueryInvoicePage(invoiceMetadataQueryRequest, pageOffset, pageSize, cancellationToken);
    }

    [McpServerTool(Name = "get_invoices_for_buyer_by_nip", Title = "Pobierz faktury dla kupującego o numerze NIP")]
    [Description("Wyszukuje faktury wystawione dla polskiego nabywcy o podanym NIP. " + SearchScopeDescription + " " + SearchResultDescription)]
    public async Task<McpPagedInvoiceResponse> GetInvoiceByBuyerNip(
        [Description("NIP nabywcy – 10 cyfr, bez kresek, spacji i prefiksu PL")] string nip,
        CancellationToken cancellationToken,
        [Description(InvoiceSubjectTypeDescription)] InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1,
        [Description(DateTypeDescription)] DateType dateType = DateType.Issue,
        [Description(PageOffsetDescription)] int pageOffset = 0,
        [Description(PageSizeDescription)] int pageSize = DefaultPageSize)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByBuyerNip)} called nip: {nip} invoiceSubjectType: {invoiceSubjectType} dateType: {dateType} pageOffset: {pageOffset} pageSize: {pageSize}");

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = invoiceSubjectType,
            BuyerIdentifier = new BuyerIdentifier
            {
                Type = BuyerIdentifierType.Nip,
                Value = nip
            },
            DateRange = GetMaxDataRange(dateType)
        };

        return await QueryInvoicePage(invoiceMetadataQueryRequest, pageOffset, pageSize, cancellationToken);
    }

    [McpServerTool(Name = "get_invoices_for_buyer_by_vateu", Title = "Pobierz faktury dla kupującego o numerze VAT UE")]
    [Description("Wyszukuje faktury wystawione dla nabywcy z innego kraju UE o podanym numerze VAT UE. " + SearchScopeDescription + " " + SearchResultDescription)]
    public async Task<McpPagedInvoiceResponse> GetInvoiceByBuyerVatUe(
        [Description("Numer VAT UE nabywcy z kodem kraju, bez spacji, np. DE123456789")] string vatUe,
        CancellationToken cancellationToken,
        [Description(InvoiceSubjectTypeDescription)] InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1,
        [Description(DateTypeDescription)] DateType dateType = DateType.Issue,
        [Description(PageOffsetDescription)] int pageOffset = 0,
        [Description(PageSizeDescription)] int pageSize = DefaultPageSize)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceByBuyerVatUe)} called vatUe: {vatUe} invoiceSubjectType: {invoiceSubjectType} dateType: {dateType} pageOffset: {pageOffset} pageSize: {pageSize}");

        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = invoiceSubjectType,
            BuyerIdentifier = new BuyerIdentifier
            {
                Type = BuyerIdentifierType.VatUe,
                Value = vatUe
            },
            DateRange = GetMaxDataRange(dateType)
        };

        return await QueryInvoicePage(invoiceMetadataQueryRequest, pageOffset, pageSize, cancellationToken);
    }

    /// <summary>
    /// Validates the paging parameters and queries one page of invoice metadata.
    /// Errors (invalid paging or a failed KSeF call) are returned in the response, not thrown.
    /// </summary>
    private async Task<McpPagedInvoiceResponse> QueryInvoicePage(InvoiceQueryFilters filters, int pageOffset, int pageSize, CancellationToken cancellationToken)
    {
        if (pageOffset < 0)
        {
            return new McpPagedInvoiceResponse($"Nieprawidłowy numer strony pageOffset: {pageOffset}. Podaj wartość 0 lub większą.");
        }
        if (pageSize < MinPageSize || pageSize > MaxPageSize)
        {
            return new McpPagedInvoiceResponse($"Nieprawidłowy rozmiar strony pageSize: {pageSize}. Podaj wartość od {MinPageSize} do {MaxPageSize}.");
        }

        try
        {
            await _ksefAuthorization.VerifyAuthToken(cancellationToken);

            var invoiceList = await _ksefClient.QueryInvoiceMetadataAsync(
                requestPayload: filters,
                accessToken: _ksefAuthorization.GetAuthenticationInfo().AccessToken.Token,
                pageOffset: pageOffset,
                pageSize: pageSize,
                cancellationToken: cancellationToken);
            return new McpPagedInvoiceResponse(invoiceList);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QueryInvoiceMetadataAsync");
            return new McpPagedInvoiceResponse($"{KsefError}{ex.Message}");
        }
    }

    [McpServerTool(Name = "get_invoice_url_by_ksef", Title = "Pobierz link do faktury po numerze ksef")]
    [Description("Zwraca link weryfikacyjny do faktury w KSeF (ten sam, który zawiera kod QR na fakturze), pod którym można sprawdzić, że faktura istnieje w KSeF. " + SearchScopeDescription + " Zwraca pusty tekst, gdy faktury nie znaleziono, albo komunikat błędu, gdy zapytanie do KSeF się nie powiodło.")]
    public async Task<string> GetInvoiceUrl([Description(KsefNumberDescription)] string ksefNumber, CancellationToken cancellationToken, [Description(InvoiceSubjectTypeDescription)] InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, [Description(DateTypeDescription)] DateType dateType = DateType.Issue)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceUrl)} called ksefNumber: {ksefNumber} invoiceSubjectType: {invoiceSubjectType} dateType: {dateType}");

        var (url, error) = await BuildInvoiceUrl(ksefNumber, cancellationToken, invoiceSubjectType, dateType);
        return error ?? url;
    }

    /// <summary>
    /// Builds the verification URL for an invoice. Returns an empty Url when the invoice was not found,
    /// or a non-null Error when authentication or the KSeF query failed.
    /// </summary>
    private async Task<(string Url, string? Error)> BuildInvoiceUrl(string ksefNumber, CancellationToken cancellationToken, InvoiceSubjectType invoiceSubjectType, DateType dateType)
    {
        var invoiceMetadataQueryRequest = new InvoiceQueryFilters
        {
            SubjectType = invoiceSubjectType,
            KsefNumber = ksefNumber,
            DateRange = GetMaxDataRange(dateType)
        };
        
        _logger.LogDebug($"invoiceMetadataQueryRequest created for ksefNumber: {ksefNumber} DateRange from {invoiceMetadataQueryRequest.DateRange.From} to {invoiceMetadataQueryRequest.DateRange.To}");

        PagedInvoiceResponse metadata;
        try
        {
            await _ksefAuthorization.VerifyAuthToken(cancellationToken);

            metadata = await _ksefClient.QueryInvoiceMetadataAsync(
                requestPayload: invoiceMetadataQueryRequest,
                accessToken: _ksefAuthorization.GetAuthenticationInfo().AccessToken.Token,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invoice metadata");
            return (string.Empty, $"{KsefError}{ex.Message}");
        }

        // KSeF may match the number case-insensitively, so compare the same way to avoid missing the invoice.
        var invoiceMetadata = metadata?.Invoices?.FirstOrDefault(x =>
            string.Equals(x.KsefNumber, ksefNumber?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (invoiceMetadata == null)
        {
            _logger.LogWarning($"No invoice metadata found for ksefNumber: {ksefNumber}");
            return (string.Empty, null);
        }

        var invoiceHash = invoiceMetadata.InvoiceHash;
        var issueDate = invoiceMetadata.IssueDate;
        
        // The verification link is built from the seller's NIP. For our own sales invoices it may be
        // missing from the metadata, so fall back to our NIP; for other invoices that would give a wrong link.
        var sellerNip = invoiceMetadata.Seller?.Nip;
        if (string.IsNullOrEmpty(sellerNip))
        {
            if (invoiceSubjectType != InvoiceSubjectType.Subject1)
            {
                _logger.LogWarning($"Seller NIP missing in metadata for ksefNumber: {ksefNumber}");
                return (string.Empty, $"Brak NIP sprzedawcy w danych faktury o numerze KSeF: {ksefNumber}");
            }
            sellerNip = _vatId;
        }

        var invoiceForOnlineUrl = _verificationLinkService.BuildInvoiceVerificationUrl(sellerNip, issueDate.DateTime, invoiceHash);

        return (invoiceForOnlineUrl, null);
    }
    
    [McpServerTool(Name = "get_invoice_qr_ksef", Title = "Pobierz qr kod do faktury po numerze ksef")]
    [Description("Generuje kod QR do weryfikacji faktury w KSeF jako obraz PNG podpisany numerem KSeF – taki kod umieszcza się na wydruku lub wizualizacji faktury. Kod prowadzi do linku zwracanego przez get_invoice_url_by_ksef. " + SearchScopeDescription + " Gdy faktury nie znaleziono lub wystąpił błąd, zwraca komunikat tekstowy zamiast obrazu.")]
    public async Task<IEnumerable<ContentBlock>> GetInvoiceQrWithKsef([Description(KsefNumberDescription)] string ksefNumber, CancellationToken cancellationToken, [Description(InvoiceSubjectTypeDescription)] InvoiceSubjectType invoiceSubjectType = InvoiceSubjectType.Subject1, [Description(DateTypeDescription)] DateType dateType = DateType.Issue)
    {
        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceQrWithKsef)} called ksefNumber: {ksefNumber} invoiceSubjectType: {invoiceSubjectType} dateType: {dateType}");
        
        var (url, error) = await BuildInvoiceUrl(ksefNumber, cancellationToken, invoiceSubjectType, dateType);
        if (error != null) return GetErroredContentInfo(error);
        if (string.IsNullOrEmpty(url)) return GetErroredContentInfo($"Nie udało się pobrać danych dla numeru KSeF: {ksefNumber}");
        
        var qrCode = QrCodeService.GenerateQrCode(url);
        if (qrCode.Length == 0) return GetErroredContentInfo($"Nie udało wygenerować kodu QR dla numeru KSeF: {ksefNumber}");
        
        var labeledQr = QrCodeService.AddLabelToQrCode(qrCode, ksefNumber);
        if (labeledQr.Length == 0) return GetErroredContentInfo($"Nie udało podpisać QR kody numerem KSeF: {ksefNumber}");
        
        // FromBytes base64-encodes the PNG, as the MCP image content requires.
        var image = ImageContentBlock.FromBytes(labeledQr, "image/png");
        image.Annotations = new Annotations { Audience = [Role.User], Priority = 0.5f };
        var contents = new List<ContentBlock> { image };

        _logger.LogInformation($"{AppConsts.KsefToolName}.{nameof(GetInvoiceQrWithKsef)} ret: {labeledQr.Length} bytes");
        return contents;
    }
    
    private static List<ContentBlock> GetErroredContentInfo(string errorInfo)
    {
        var contents = new List<ContentBlock>
        {
            new TextContentBlock {
                Text = errorInfo,
                Annotations = new Annotations { Audience = [Role.User], Priority = 0.7f }
            }
        };
        return contents;
    }

    private static DateRange GetMaxDataRange(DateType dateType)
    {
        return new DateRange
        {
            From = DateTime.UtcNow.AddMonths(-3).AddMinutes(1),
            To = DateTime.UtcNow,
            DateType = dateType
        };
    }
}

