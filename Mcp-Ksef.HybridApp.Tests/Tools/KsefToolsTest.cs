using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using McpKsef.HybridApp.Services;
using McpKsef.HybridApp.Tools;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Moq;
using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Tools;

public class KsefToolsTest : IDisposable
{
    private readonly Mock<ILogger<KsefTools>> _loggerMock;
    private readonly Mock<IKsefAuthorizationService> _authorizationServiceMock;
    private readonly Mock<IKSeFClient> _ksefClientMock;
    private readonly Mock<IVerificationLinkService> _verificationLinkServiceMock;
    private readonly string _originalVatId;

    public KsefToolsTest()
    {
        _loggerMock = new Mock<ILogger<KsefTools>>();
        _authorizationServiceMock = new Mock<IKsefAuthorizationService>();
        _ksefClientMock = new Mock<IKSeFClient>();
        _verificationLinkServiceMock = new Mock<IVerificationLinkService>();
        _originalVatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, "1234567890");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, _originalVatId);
    }

    private KsefTools CreateService()
    {
        var authInfo = new AuthenticationOperationStatusResponse
        {
            AccessToken = new TokenInfo { Token = "test-token" }
        };
        _authorizationServiceMock.Setup(x => x.GetAuthenticationInfo()).Returns(authInfo);
        
        return new KsefTools(
            _loggerMock.Object,
            _authorizationServiceMock.Object,
            _ksefClientMock.Object,
            _verificationLinkServiceMock.Object);
    }

    [Fact]
    public async Task GetInvoice_WithValidKsefNumber_ReturnsInvoiceString()
    {
        var expectedInvoice = "<invoice>test invoice xml</invoice>";
        _ksefClientMock
            .Setup(x => x.GetInvoiceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInvoice);
    
        var service = CreateService();
    
        var result = await service.GetInvoice("1234567890-TEST", CancellationToken.None);
    
        Assert.Equal(expectedInvoice, result);
        _authorizationServiceMock.Verify(x => x.VerifyAuthToken(It.IsAny<CancellationToken>()), Times.Once);
        _ksefClientMock.Verify(x => x.GetInvoiceAsync("1234567890-TEST", "test-token", It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoice_VerifiesAuthTokenBeforeCall()
    {
        var callOrder = new List<string>();
        _authorizationServiceMock
            .Setup(x => x.VerifyAuthToken(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("VerifyAuthToken"));
        _ksefClientMock
            .Setup(x => x.GetInvoiceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("GetInvoiceAsync"))
            .ReturnsAsync("<invoice></invoice>");
    
        var service = CreateService();
        await service.GetInvoice("TEST-123", CancellationToken.None);
    
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("VerifyAuthToken", callOrder[0]);
        Assert.Equal("GetInvoiceAsync", callOrder[1]);
    }
    
    [Fact]
    public async Task GetInvoicesListForGivenDate_WithValidDateRange_ReturnsPagedResponse()
    {
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 1, 31);
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new() { KsefNumber = "INV-001" },
                new() { KsefNumber = "INV-002" }
            }
        };
        
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoicesListForGivenDate(fromDate, toDate, CancellationToken.None);
    
        Assert.Equal(2, result.Invoices.Count);
        _authorizationServiceMock.Verify(x => x.VerifyAuthToken(It.IsAny<CancellationToken>()), Times.Once);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.DateRange.From == fromDate && f.DateRange.To == toDate && f.SubjectType == InvoiceSubjectType.Subject1), 
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoicesListForGivenDate_WithSameFromAndToDate_QueriesSingleDay()
    {
        var singleDate = new DateTime(2026, 1, 1);
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new() { KsefNumber = "INV-001" },
                new() { KsefNumber = "INV-002" }
            }
        };
        
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoicesListForGivenDate(singleDate, singleDate, CancellationToken.None);
    
        Assert.Equal(2, result.Invoices.Count);
        _authorizationServiceMock.Verify(x => x.VerifyAuthToken(It.IsAny<CancellationToken>()), Times.Once);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.DateRange.From == singleDate && f.DateRange.To == singleDate && f.SubjectType == InvoiceSubjectType.Subject1), 
            It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByInvoiceNumber_WithValidNumber_ReturnsPagedResponse()
    {
        var invoiceNumber = "FV/2026/01/001";
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new () { KsefNumber = "KSEF-123", InvoiceNumber = invoiceNumber }
            }
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceByInvoiceNumber(invoiceNumber, CancellationToken.None);
    
        Assert.Single(result.Invoices);
        Assert.Equal(invoiceNumber, result.Invoices.ToArray()[0].InvoiceNumber);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f =>
                f.InvoiceNumber == invoiceNumber &&
                f.DateRange != null), 
            It.IsAny<string>(), 
            It.IsAny<int?>(), 
            It.IsAny<int?>(), 
            It.IsAny<SortOrder>(), 
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByInvoiceNumber_UsesMaxDateRange()
    {
        var invoiceNumber = "FV/2026/01/001";
        var expectedResponse = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        await service.GetInvoiceByInvoiceNumber(invoiceNumber, CancellationToken.None);
        
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
                It.Is<InvoiceQueryFilters>(f => 
                    f.InvoiceNumber == invoiceNumber &&
                    f.DateRange != null && f.DateRange.DateType == DateType.Issue &&
                    f.DateRange.To <= DateTime.UtcNow &&
                    f.DateRange.From < f.DateRange.To), 
                It.IsAny<string>(), 
                It.IsAny<int?>(), 
                It.IsAny<int?>(), 
                It.IsAny<SortOrder>(), 
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByBuyerNip_WithValidNip_ReturnsInvoices()
    {
        var buyerNip = "9876543210";
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new InvoiceSummary { KsefNumber = "INV-001" }
            }
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceByBuyerNip(buyerNip, CancellationToken.None);
    
        Assert.Single(result.Invoices);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => 
                f.BuyerIdentifier != null &&
                f.BuyerIdentifier.Type == BuyerIdentifierType.Nip &&
                f.BuyerIdentifier.Value == buyerNip),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<SortOrder>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByBuyerVatUe_WithValidVatUe_ReturnsInvoices()
    {
        var vatUe = "DE123456789";
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new InvoiceSummary { KsefNumber = "INV-EU-001" }
            }
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceByBuyerVatUe(vatUe, CancellationToken.None);
    
        Assert.Single(result.Invoices);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => 
                f.BuyerIdentifier != null &&
                f.BuyerIdentifier.Type == BuyerIdentifierType.VatUe &&
                f.BuyerIdentifier.Value == vatUe),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<SortOrder>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceUrl_WithValidKsefNumber_ReturnsVerificationUrl()
    {
        var ksefNumber = "1234567890-TEST";
        var invoiceHash = "hash123";
        var invoicingDate = DateTimeOffset.UtcNow;
        var expectedUrl = "https://ksef.mf.gov.pl/verify?hash=hash123";
    
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new()
                { 
                    KsefNumber = ksefNumber,
                    InvoiceHash = invoiceHash,
                    InvoicingDate = invoicingDate
                }
            }
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        _verificationLinkServiceMock
            .Setup(x => x.BuildInvoiceVerificationUrl(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(expectedUrl);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceUrl(ksefNumber, CancellationToken.None);
    
        Assert.Equal(expectedUrl, result);
        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            "1234567890",
            invoicingDate.DateTime,
            invoiceHash), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceUrl_WhenMetadataIsNull_ReturnsEmptyString()
    {
        var ksefNumber = "NON-EXISTENT";
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync((PagedInvoiceResponse)null);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceUrl(ksefNumber, CancellationToken.None);
    
        Assert.Empty(result);
    }
    
    [Fact]
    public async Task GetInvoiceUrl_WhenInvoicesListIsEmpty_ReturnsEmptyString()
    {
        var ksefNumber = "NON-EXISTENT";
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>()
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceUrl(ksefNumber, CancellationToken.None);
    
        Assert.Empty(result);
    }
    
    [Fact]
    public async Task GetInvoiceUrl_WithMultipleInvoices_ReturnsCorrectOne()
    {
        var ksefNumber = "KSEF-TARGET";
        var targetHash = "target-hash";
        var targetDate = DateTimeOffset.UtcNow.AddDays(-1);
    
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new()
                { 
                    KsefNumber = "KSEF-OTHER-1",
                    InvoiceHash = "other-hash-1",
                    InvoicingDate = DateTimeOffset.UtcNow
                },
                new()
                { 
                    KsefNumber = ksefNumber,
                    InvoiceHash = targetHash,
                    InvoicingDate = targetDate
                },
                new()
                { 
                    KsefNumber = "KSEF-OTHER-2",
                    InvoiceHash = "other-hash-2",
                    InvoicingDate = DateTimeOffset.UtcNow.AddDays(-2)
                }
            }
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        _verificationLinkServiceMock
            .Setup(x => x.BuildInvoiceVerificationUrl(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns("https://url.com");
    
        var service = CreateService();
    
        await service.GetInvoiceUrl(ksefNumber, CancellationToken.None);
    
        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            It.IsAny<string>(),
            targetDate.DateTime,
            targetHash), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceQrWithKsef_WithValidKsefNumber_ReturnsImageContent()
    {
        var ksefNumber = "1234567890-TEST";
        var invoiceHash = "hash123";
        var invoicingDate = DateTimeOffset.UtcNow;
        var expectedUrl = "https://ksef.mf.gov.pl/verify";
    
        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new InvoiceSummary 
                { 
                    KsefNumber = ksefNumber,
                    InvoiceHash = invoiceHash,
                    InvoicingDate = invoicingDate
                }
            }
        };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        _verificationLinkServiceMock
            .Setup(x => x.BuildInvoiceVerificationUrl(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns(expectedUrl);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceQrWithKsef(ksefNumber, CancellationToken.None);
    
        var contentList = result.ToList();
        Assert.Single(contentList);
        Assert.IsType<ImageContentBlock>(contentList[0]);
        var imageContent = (ImageContentBlock)contentList[0];
        Assert.Equal("image/png", imageContent.MimeType);
        Assert.NotEmpty(imageContent.Data);
    }
    
    [Fact]
    public async Task GetInvoiceQrWithKsef_WhenUrlIsEmpty_ReturnsErrorMessage()
    {
        var ksefNumber = "INVALID";
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync((PagedInvoiceResponse)null);
    
        var service = CreateService();
    
        var result = await service.GetInvoiceQrWithKsef(ksefNumber, CancellationToken.None);
    
        var contentList = result.ToList();
        Assert.Single(contentList);
        Assert.IsType<TextContentBlock>(contentList[0]);
        var textContent = (TextContentBlock)contentList[0];
        Assert.Contains("Nie udało się pobrać danych", textContent.Text);
        Assert.Contains(ksefNumber, textContent.Text);
    }
    
    [Fact]
    public async Task GetInvoiceByBuyerNip_EmptyNip_StillQueriesWithEmptyValue()
    {
        var emptyNip = "";
        var expectedResponse = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        await service.GetInvoiceByBuyerNip(emptyNip, CancellationToken.None);
    
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => 
                f.BuyerIdentifier != null &&
                f.BuyerIdentifier.Value == emptyNip),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<SortOrder>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByBuyerVatUe_EmptyVatUe_StillQueriesWithEmptyValue()
    {
        var emptyVatUe = "";
        var expectedResponse = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        await service.GetInvoiceByBuyerVatUe(emptyVatUe, CancellationToken.None);
    
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => 
                f.BuyerIdentifier != null &&
                f.BuyerIdentifier.Value == emptyVatUe),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<SortOrder>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoice_WithCancellationToken_PassesTokenToClient()
    {
        var cts = new CancellationTokenSource();
        var expectedInvoice = "<invoice></invoice>";
        
        _ksefClientMock
            .Setup(x => x.GetInvoiceAsync(It.IsAny<string>(), It.IsAny<string>(), cts.Token))
            .ReturnsAsync(expectedInvoice);
    
        var service = CreateService();
    
        await service.GetInvoice("TEST", cts.Token);
    
        _ksefClientMock.Verify(x => x.GetInvoiceAsync(It.IsAny<string>(), It.IsAny<string>(), cts.Token), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoicesListForGivenDate_WithFutureDates_StillProcessesRequest()
    {
        var futureStart = DateTime.UtcNow.AddMonths(1);
        var futureEnd = DateTime.UtcNow.AddMonths(2);
        var expectedResponse = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
    
        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(expectedResponse);
    
        var service = CreateService();
    
        var result = await service.GetInvoicesListForGivenDate(futureStart, futureEnd, CancellationToken.None);
    
        Assert.NotNull(result);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.DateRange.From == futureStart && f.DateRange.To == futureEnd),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<SortOrder>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
