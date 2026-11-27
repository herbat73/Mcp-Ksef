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

[Collection(EnvironmentVariablesCollection.Name)]
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
    
        var result = await service.QueryInvoices(fromDate, toDate, CancellationToken.None);

        Assert.IsType<McpPagedInvoiceResponse>(result);
        Assert.False(result.Error);
        Assert.Null(result.ErrorMessage);
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
    
        var result = await service.QueryInvoices(singleDate, singleDate, CancellationToken.None);
    
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
    
        Assert.False(result.Error);
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

        Assert.False(result.Error);
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

        Assert.False(result.Error);
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
        // Issue and invoicing dates differ, so the test shows the link uses the issue date.
        var issueDate = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var invoicingDate = new DateTimeOffset(2026, 1, 20, 10, 30, 0, TimeSpan.Zero);
        var expectedUrl = "https://ksef.mf.gov.pl/verify?hash=hash123";

        var expectedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new()
                {
                    KsefNumber = ksefNumber,
                    InvoiceHash = invoiceHash,
                    IssueDate = issueDate,
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
            issueDate.DateTime,
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
                    IssueDate = DateTimeOffset.UtcNow
                },
                new()
                {
                    KsefNumber = ksefNumber,
                    InvoiceHash = targetHash,
                    IssueDate = targetDate
                },
                new()
                {
                    KsefNumber = "KSEF-OTHER-2",
                    InvoiceHash = "other-hash-2",
                    IssueDate = DateTimeOffset.UtcNow.AddDays(-2)
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
        Assert.Equal("image/png;base64", imageContent.MimeType);
        Assert.True(imageContent.Data.Length>0);
    }
    
    [Fact]
    public async Task GetInvoiceQrWithKsef_Result_SerializesAsCallToolResult()
    {
        // Regression: ModelContextProtocol 2.4.0 failed to serialize annotated content blocks
        // (csharp-sdk issue #2012) with "Cannot insert the values of 'TypeInfoResolver'...".
        var invoicingDate = DateTimeOffset.UtcNow;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedInvoiceResponse
            {
                Invoices = new List<InvoiceSummary>
                {
                    new() { KsefNumber = "1234567890-TEST", InvoiceHash = "hash123", InvoicingDate = invoicingDate }
                }
            });
        _verificationLinkServiceMock
            .Setup(x => x.BuildInvoiceVerificationUrl(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns("https://ksef.mf.gov.pl/verify");
        var service = CreateService();

        var content = await service.GetInvoiceQrWithKsef("1234567890-TEST", CancellationToken.None);
        var callToolResult = new CallToolResult { Content = content.ToList() };

        // The MCP server serializes tool results through the SDK's internal source-generated context
        // (McpJsonUtilities.JsonContext); the bug only reproduces on that path, not with DefaultOptions.
        var contextType = typeof(ModelContextProtocol.McpJsonUtilities).GetNestedType(
            "JsonContext", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(contextType);
        var context = (System.Text.Json.Serialization.JsonSerializerContext)contextType!
            .GetProperty("Default", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(null)!;

        var json = System.Text.Json.JsonSerializer.SerializeToNode(
            callToolResult, context.GetTypeInfo(typeof(CallToolResult))!);

        Assert.NotNull(json);
        Assert.Equal("image", json!["content"]![0]!["type"]!.GetValue<string>());
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
    
        var result = await service.QueryInvoices(futureStart, futureEnd, CancellationToken.None);
    
        Assert.NotNull(result);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.DateRange.From == futureStart && f.DateRange.To == futureEnd),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<int?>(),
            It.IsAny<SortOrder>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private const string KsefErrorPrefix = "Wystąpił nieoczekiwany błąd podczas wykonania zapytania do KSeF - ";

    private void SetupQueryInvoiceMetadataThrows(Exception exception)
    {
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
    }

    private static void AssertErrorResponse(McpPagedInvoiceResponse result, string exceptionMessage)
    {
        Assert.NotNull(result);
        Assert.True(result.Error);
        Assert.Equal($"{KsefErrorPrefix}{exceptionMessage}", result.ErrorMessage);
    }

    [Fact]
    public async Task QueryInvoices_WhenClientThrows_ReturnsErrorResponse()
    {
        SetupQueryInvoiceMetadataThrows(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.QueryInvoices(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        AssertErrorResponse(result, "boom");
    }

    [Fact]
    public async Task GetInvoiceByInvoiceNumber_WhenClientThrows_ReturnsErrorResponse()
    {
        SetupQueryInvoiceMetadataThrows(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None);

        AssertErrorResponse(result, "boom");
    }

    [Fact]
    public async Task GetInvoiceByBuyerNip_WhenClientThrows_ReturnsErrorResponse()
    {
        SetupQueryInvoiceMetadataThrows(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None);

        AssertErrorResponse(result, "boom");
    }

    [Fact]
    public async Task GetInvoiceByBuyerVatUe_WhenClientThrows_ReturnsErrorResponse()
    {
        SetupQueryInvoiceMetadataThrows(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None);

        AssertErrorResponse(result, "boom");
    }

    [Fact]
    public async Task GetInvoiceUrl_WhenClientThrows_ReturnsErrorMessageWithExceptionMessage()
    {
        SetupQueryInvoiceMetadataThrows(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None);

        Assert.Equal($"{KsefErrorPrefix}boom", result);
        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetInvoiceQrWithKsef_WhenClientThrows_ReturnsErrorTextInsteadOfQr()
    {
        SetupQueryInvoiceMetadataThrows(new InvalidOperationException("boom"));
        var service = CreateService();

        var result = await service.GetInvoiceQrWithKsef("1234567890-TEST", CancellationToken.None);

        var contentList = result.ToList();
        Assert.Single(contentList);
        var textContent = Assert.IsType<TextContentBlock>(contentList[0]);
        Assert.Contains($"{KsefErrorPrefix}boom", textContent.Text);
        Assert.DoesNotContain(contentList, c => c is ImageContentBlock);
    }

    public static TheoryData<DateType> AllDateTypes()
    {
        var data = new TheoryData<DateType>();
        foreach (var dateType in Enum.GetValues<DateType>())
        {
            data.Add(dateType);
        }
        return data;
    }

    private Func<InvoiceQueryFilters?> CaptureQueryFilters()
    {
        InvoiceQueryFilters? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
            .ReturnsAsync(new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() });
        return () => captured;
    }

    [Theory]
    [MemberData(nameof(AllDateTypes))]
    public async Task GetInvoiceByInvoiceNumber_PassesDateTypeToQuery(DateType dateType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None, dateType: dateType);

        Assert.Equal(dateType, captured()!.DateRange.DateType);
    }

    [Theory]
    [MemberData(nameof(AllDateTypes))]
    public async Task GetInvoiceByBuyerNip_PassesDateTypeToQuery(DateType dateType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None, dateType: dateType);

        Assert.Equal(dateType, captured()!.DateRange.DateType);
    }

    [Theory]
    [MemberData(nameof(AllDateTypes))]
    public async Task GetInvoiceByBuyerVatUe_PassesDateTypeToQuery(DateType dateType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None, dateType: dateType);

        Assert.Equal(dateType, captured()!.DateRange.DateType);
    }

    [Theory]
    [MemberData(nameof(AllDateTypes))]
    public async Task GetInvoiceUrl_PassesDateTypeToQuery(DateType dateType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None, dateType: dateType);

        Assert.Equal(dateType, captured()!.DateRange.DateType);
    }

    [Theory]
    [MemberData(nameof(AllDateTypes))]
    public async Task GetInvoiceQrWithKsef_PassesDateTypeToQuery(DateType dateType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceQrWithKsef("1234567890-TEST", CancellationToken.None, dateType: dateType);

        Assert.Equal(dateType, captured()!.DateRange.DateType);
    }

    public static TheoryData<InvoiceSubjectType> AllInvoiceSubjectTypes()
    {
        var data = new TheoryData<InvoiceSubjectType>();
        foreach (var subjectType in Enum.GetValues<InvoiceSubjectType>())
        {
            data.Add(subjectType);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllInvoiceSubjectTypes))]
    public async Task GetInvoiceByInvoiceNumber_PassesInvoiceSubjectTypeToQuery(InvoiceSubjectType subjectType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None, invoiceSubjectType: subjectType);

        Assert.Equal(subjectType, captured()!.SubjectType);
    }

    [Theory]
    [MemberData(nameof(AllInvoiceSubjectTypes))]
    public async Task GetInvoiceByBuyerNip_PassesInvoiceSubjectTypeToQuery(InvoiceSubjectType subjectType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None, invoiceSubjectType: subjectType);

        Assert.Equal(subjectType, captured()!.SubjectType);
    }

    [Theory]
    [MemberData(nameof(AllInvoiceSubjectTypes))]
    public async Task GetInvoiceByBuyerVatUe_PassesInvoiceSubjectTypeToQuery(InvoiceSubjectType subjectType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None, invoiceSubjectType: subjectType);

        Assert.Equal(subjectType, captured()!.SubjectType);
    }

    private void SetupSingleInvoiceMetadata(string ksefNumber, string? sellerNip)
    {
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedInvoiceResponse
            {
                Invoices = new List<InvoiceSummary>
                {
                    new()
                    {
                        KsefNumber = ksefNumber,
                        InvoiceHash = "hash123",
                        InvoicingDate = DateTimeOffset.UtcNow,
                        Seller = sellerNip == null ? null : new Seller { Nip = sellerNip }
                    }
                }
            });
        _verificationLinkServiceMock
            .Setup(x => x.BuildInvoiceVerificationUrl(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns("https://ksef.mf.gov.pl/verify");
    }

    [Fact]
    public async Task GetInvoiceUrl_PurchaseInvoice_UsesSellerNipFromMetadata()
    {
        SetupSingleInvoiceMetadata("5555555555-TEST", sellerNip: "5555555555");
        var service = CreateService();

        var result = await service.GetInvoiceUrl("5555555555-TEST", CancellationToken.None, InvoiceSubjectType.Subject2);

        Assert.Equal("https://ksef.mf.gov.pl/verify", result);
        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            "5555555555", It.IsAny<DateTime>(), "hash123"), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceUrl_SalesInvoiceWithSellerNip_UsesSellerNipFromMetadata()
    {
        SetupSingleInvoiceMetadata("1234567890-TEST", sellerNip: "1234567890");
        var service = CreateService();

        await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None);

        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            "1234567890", It.IsAny<DateTime>(), "hash123"), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceUrl_SalesInvoiceWithoutSellerNip_FallsBackToOurNip()
    {
        Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, "9999999999");
        SetupSingleInvoiceMetadata("1234567890-TEST", sellerNip: null);
        var service = CreateService();

        await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None);

        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            "9999999999", It.IsAny<DateTime>(), "hash123"), Times.Once);
    }

    [Theory]
    [InlineData(InvoiceSubjectType.Subject2)]
    [InlineData(InvoiceSubjectType.Subject3)]
    [InlineData(InvoiceSubjectType.SubjectAuthorized)]
    public async Task GetInvoiceUrl_NonSalesInvoiceWithoutSellerNip_ReturnsErrorAndDoesNotUseOurNip(InvoiceSubjectType subjectType)
    {
        SetupSingleInvoiceMetadata("5555555555-TEST", sellerNip: null);
        var service = CreateService();

        var result = await service.GetInvoiceUrl("5555555555-TEST", CancellationToken.None, subjectType);

        Assert.Contains("Brak NIP sprzedawcy", result);
        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetInvoiceQrWithKsef_PurchaseInvoice_UsesSellerNipAndReturnsImage()
    {
        SetupSingleInvoiceMetadata("5555555555-TEST", sellerNip: "5555555555");
        var service = CreateService();

        var result = (await service.GetInvoiceQrWithKsef("5555555555-TEST", CancellationToken.None, InvoiceSubjectType.Subject2)).ToList();

        Assert.IsType<ImageContentBlock>(Assert.Single(result));
        _verificationLinkServiceMock.Verify(x => x.BuildInvoiceVerificationUrl(
            "5555555555", It.IsAny<DateTime>(), "hash123"), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceQrWithKsef_PurchaseInvoiceWithoutSellerNip_ReturnsErrorText()
    {
        SetupSingleInvoiceMetadata("5555555555-TEST", sellerNip: null);
        var service = CreateService();

        var result = (await service.GetInvoiceQrWithKsef("5555555555-TEST", CancellationToken.None, InvoiceSubjectType.Subject2)).ToList();

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result));
        Assert.Contains("Brak NIP sprzedawcy", text.Text);
    }

    [Theory]
    [MemberData(nameof(AllInvoiceSubjectTypes))]
    public async Task GetInvoiceUrl_PassesInvoiceSubjectTypeToQuery(InvoiceSubjectType subjectType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None, subjectType);

        Assert.Equal(subjectType, captured()!.SubjectType);
    }

    [Theory]
    [MemberData(nameof(AllInvoiceSubjectTypes))]
    public async Task GetInvoiceQrWithKsef_PassesInvoiceSubjectTypeToQuery(InvoiceSubjectType subjectType)
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceQrWithKsef("1234567890-TEST", CancellationToken.None, subjectType);

        Assert.Equal(subjectType, captured()!.SubjectType);
    }

    [Fact]
    public async Task SearchMethods_WithoutInvoiceSubjectType_DefaultToSubject1()
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None);
        Assert.Equal(InvoiceSubjectType.Subject1, captured()!.SubjectType);

        await service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None);
        Assert.Equal(InvoiceSubjectType.Subject1, captured()!.SubjectType);

        await service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None);
        Assert.Equal(InvoiceSubjectType.Subject1, captured()!.SubjectType);

        await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None);
        Assert.Equal(InvoiceSubjectType.Subject1, captured()!.SubjectType);

        await service.GetInvoiceQrWithKsef("1234567890-TEST", CancellationToken.None);
        Assert.Equal(InvoiceSubjectType.Subject1, captured()!.SubjectType);
    }

    [Fact]
    public async Task QueryMethods_WithoutDateType_DefaultToIssue()
    {
        var captured = CaptureQueryFilters();
        var service = CreateService();

        await service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None);
        Assert.Equal(DateType.Issue, captured()!.DateRange.DateType);

        await service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None);
        Assert.Equal(DateType.Issue, captured()!.DateRange.DateType);

        await service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None);
        Assert.Equal(DateType.Issue, captured()!.DateRange.DateType);

        await service.GetInvoiceUrl("1234567890-TEST", CancellationToken.None);
        Assert.Equal(DateType.Issue, captured()!.DateRange.DateType);

        await service.GetInvoiceQrWithKsef("1234567890-TEST", CancellationToken.None);
        Assert.Equal(DateType.Issue, captured()!.DateRange.DateType);
    }

    private Func<(int? PageOffset, int? PageSize)?> CapturePaging()
    {
        (int?, int?)? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((_, _, offset, size, _, _) => captured = (offset, size))
            .ReturnsAsync(new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() });
        return () => captured;
    }

    /// <summary>Calls one of the four paged search tools by name.</summary>
    private static Task<McpPagedInvoiceResponse> CallSearch(KsefTools service, string method, int? pageOffset = null, int? pageSize = null)
    {
        var offset = pageOffset ?? 0;
        var size = pageSize ?? 10;
        var useDefaults = pageOffset == null && pageSize == null;
        return method switch
        {
            nameof(KsefTools.QueryInvoices) => useDefaults
                ? service.QueryInvoices(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None)
                : service.QueryInvoices(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None, pageOffset: offset, pageSize: size),
            nameof(KsefTools.GetInvoiceByInvoiceNumber) => useDefaults
                ? service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None)
                : service.GetInvoiceByInvoiceNumber("FV/2026/01/001", CancellationToken.None, pageOffset: offset, pageSize: size),
            nameof(KsefTools.GetInvoiceByBuyerNip) => useDefaults
                ? service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None)
                : service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None, pageOffset: offset, pageSize: size),
            nameof(KsefTools.GetInvoiceByBuyerVatUe) => useDefaults
                ? service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None)
                : service.GetInvoiceByBuyerVatUe("DE123456789", CancellationToken.None, pageOffset: offset, pageSize: size),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }

    public static TheoryData<string> SearchMethods() => new()
    {
        nameof(KsefTools.QueryInvoices),
        nameof(KsefTools.GetInvoiceByInvoiceNumber),
        nameof(KsefTools.GetInvoiceByBuyerNip),
        nameof(KsefTools.GetInvoiceByBuyerVatUe)
    };

    [Theory]
    [MemberData(nameof(SearchMethods))]
    public async Task SearchMethods_WithoutPaging_RequestFirstPageOfTen(string method)
    {
        var captured = CapturePaging();
        var service = CreateService();

        var result = await CallSearch(service, method);

        Assert.False(result.Error);
        Assert.Equal((0, 10), captured());
    }

    [Theory]
    [MemberData(nameof(SearchMethods))]
    public async Task SearchMethods_PassPagingToQuery(string method)
    {
        var captured = CapturePaging();
        var service = CreateService();

        var result = await CallSearch(service, method, pageOffset: 3, pageSize: 100);

        Assert.False(result.Error);
        Assert.Equal((3, 100), captured());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(5, 250)]
    public async Task SearchMethods_AcceptBoundaryPaging(int pageOffset, int pageSize)
    {
        var captured = CapturePaging();
        var service = CreateService();

        var result = await service.GetInvoiceByBuyerNip("9876543210", CancellationToken.None, pageOffset: pageOffset, pageSize: pageSize);

        Assert.False(result.Error);
        Assert.Equal((pageOffset, pageSize), captured());
    }

    [Theory]
    [InlineData(-1, 10, "pageOffset")]
    [InlineData(0, 9, "pageSize")]
    [InlineData(0, 251, "pageSize")]
    [InlineData(0, 0, "pageSize")]
    public async Task SearchMethods_InvalidPaging_ReturnErrorWithoutCallingKsef(int pageOffset, int pageSize, string invalidParameter)
    {
        CapturePaging();
        var service = CreateService();

        foreach (var method in SearchMethods())
        {
            var result = await CallSearch(service, method, pageOffset, pageSize);

            Assert.True(result.Error, method);
            Assert.Contains(invalidParameter, result.ErrorMessage);
        }

        _authorizationServiceMock.Verify(x => x.VerifyAuthToken(It.IsAny<CancellationToken>()), Times.Never);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.IsAny<InvoiceQueryFilters>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchMethods_PassHasMoreFromKsef()
    {
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedInvoiceResponse
            {
                Invoices = new List<InvoiceSummary> { new() { KsefNumber = "INV-001" } },
                HasMore = true
            });
        var service = CreateService();

        var result = await service.QueryInvoices(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None, pageOffset: 1);

        Assert.True(result.HasMore);
        Assert.Single(result.Invoices);
    }
}
