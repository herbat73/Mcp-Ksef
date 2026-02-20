using System.Reflection;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using KSeF.Client.Core.Models.Invoices;
using McpKsef.HybridApp.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Tools;

public class KsefToolsTest : IDisposable
{
    private readonly Mock<ILogger<KsefTools>> _loggerMock;
    private readonly Mock<IAuthorizationClient> _authClientMock;
    private readonly Mock<ICryptographyService> _cryptoServiceMock;
    private readonly Mock<IKSeFClient> _ksefClientMock;
    private readonly Mock<IVerificationLinkService> _verifyLinkMock;

    public KsefToolsTest()
    {
        _loggerMock = new Mock<ILogger<KsefTools>>();
        _authClientMock = new Mock<IAuthorizationClient>();
        _cryptoServiceMock = new Mock<ICryptographyService>();
        _ksefClientMock = new Mock<IKSeFClient>();
        _verifyLinkMock = new Mock<IVerificationLinkService>();

        ResetStaticAuthResponse();
    }

    public void Dispose()
    {
        ResetStaticAuthResponse();
    }

    private void ResetStaticAuthResponse()
    {
        var field = typeof(KsefTools).GetField("_authenticationResponse", BindingFlags.Static | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(null, null);
        }
    }

    private void SetStaticAuthResponse(string token, DateTime validUntil)
    {
        var response = new AuthenticationOperationStatusResponse
        {
            AccessToken = new TokenInfo
            {
                Token = token,
                ValidUntil = validUntil
            },
            RefreshToken = new TokenInfo
            {
                Token = "refresh-token",
                ValidUntil = validUntil.AddMinutes(30)
            }
        };

        var field = typeof(KsefTools).GetField("_authenticationResponse", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, response);
    }
    
    private KsefTools CreateSut(string? vatId = "1111111111")
    {
        // We set the VAT_ID env var for the constructor
        Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, vatId);

        return new KsefTools(
            _loggerMock.Object,
            _authClientMock.Object,
            _cryptoServiceMock.Object,
            _ksefClientMock.Object,
            _verifyLinkMock.Object
        );
    }
    
    [Fact]
    public async Task GetInvoice_AuthValid_CallsKsefClientWithToken()
    {
        // Arrange
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var ksefNum = "1234567890-20231001-123456-12";
        
        _ksefClientMock.Setup(x => x.GetInvoiceAsync(ksefNum, token, It.IsAny<CancellationToken>()))
            .ReturnsAsync("xml-content");

        // Act
        var result = await sut.GetInvoice(ksefNum, CancellationToken.None);

        // Assert
        Assert.Equal("xml-content", result);
        _ksefClientMock.Verify(x => x.GetInvoiceAsync(ksefNum, token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoicesListForGivenDate_AuthValid_CallsQueryWithDates()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var from = DateTime.UtcNow.AddDays(-10);
        var to = DateTime.UtcNow;
        
        using var cts = new CancellationTokenSource();
        InvoiceQueryFilters? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.Is<CancellationToken>(ct => ct == cts.Token)))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
            .ReturnsAsync(response)
            .Verifiable();

        // Act
        var result = await sut.GetInvoicesListForGivenDate(from, to, cts.Token);

        // Assert
        Assert.Same(response, result);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.DateRange.From == from && f.DateRange.To == to && f.SubjectType == InvoiceSubjectType.Subject1), 
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByInvoiceNumber_AuthValid_CallsQueryWithInvoiceNumber()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var invNum = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        InvoiceQueryFilters? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.Is<CancellationToken>(ct => ct == cts.Token)))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
            .ReturnsAsync(response)
            .Verifiable();

        // Act
        var result = await sut.GetInvoiceByInvoiceNumber(invNum, cts.Token);

        // Assert
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.InvoiceNumber == invNum && f.DateRange != null), 
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(),It.IsAny<CancellationToken>()), Times.Once);
        Assert.Same(response, result);
        Assert.NotNull(captured);
        Assert.Equal(InvoiceSubjectType.Subject1, captured.SubjectType);
        Assert.NotNull(captured.DateRange);
        Assert.Equal(DateType.Issue, captured.DateRange.DateType); 
        Assert.True(captured.DateRange.To >= captured.DateRange.From);
    }

    [Fact]
    public async Task GetInvoiceByBuyerNip_AuthValid_CallsQueryWithNipIdentifier()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var nip = "1010101010";
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

        using var cts = new CancellationTokenSource();
        InvoiceQueryFilters? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.Is<CancellationToken>(ct => ct == cts.Token)))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
            .ReturnsAsync(response)
            .Verifiable();

        // Act
        await sut.GetInvoiceByBuyerNip(nip, cts.Token);

        // Assert
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.BuyerIdentifier.Type == BuyerIdentifierType.Nip && f.BuyerIdentifier.Value == nip), 
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(),It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetInvoiceByBuyerVatUe_AuthValid_CallsQueryWithVatUeIdentifier()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var vatUe = "PL1010101010";
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

        using var cts = new CancellationTokenSource();
        InvoiceQueryFilters? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.Is<CancellationToken>(ct => ct == cts.Token)))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
            .ReturnsAsync(response)
            .Verifiable();

        // Act
        await sut.GetInvoiceByBuyerVatUe(vatUe, cts.Token);

        // Assert
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.BuyerIdentifier.Type == BuyerIdentifierType.VatUe && f.BuyerIdentifier.Value == vatUe), 
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(),It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceUrl_AuthValid_PullsMetadataAndBuildsUrl()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        var vatId = "1111111111";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut(vatId);
        var ksefNum = "1234567890-20231001-123456-12";
        var hash = "abc-hash";
        var date = DateTime.UtcNow;

        var pagedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new() { KsefNumber = ksefNum, InvoiceHash = hash, InvoicingDate = date }
            }
        };

        using var cts = new CancellationTokenSource();
        InvoiceQueryFilters? captured = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.Is<CancellationToken>(ct => ct == cts.Token)))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
            .ReturnsAsync(pagedResponse)
            .Verifiable();

        _verifyLinkMock.Setup(x => x.BuildInvoiceVerificationUrl(vatId, date, hash))
            .Returns("https://ksef.gov.pl/link");

        // Act
        var result = await sut.GetInvoiceUrl(ksefNum, cts.Token);

        // Assert
        Assert.Equal("https://ksef.gov.pl/link", result);
    }

    [Fact]
    public async Task AuthTokenExpired_RefreshesToken_BeforeApiCall()
    {
        // Arrange
        var oldToken = "old-token";
        var newToken = "new-token";
        // Token expires in 1 minute (logic checks for < 5 mins)
        SetStaticAuthResponse(oldToken, DateTime.Now.AddMinutes(1)); 
        
        var sut = CreateSut();

        _ksefClientMock.Setup(x => x.RefreshAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenResponse { AccessToken = new TokenInfo { Token = newToken, ValidUntil = DateTime.Now.AddHours(1) } });

        _ksefClientMock.Setup(x => x.GetInvoiceAsync(It.IsAny<string>(), newToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync("xml");

        // Act
        await sut.GetInvoice("123", CancellationToken.None);

        // Assert
        _ksefClientMock.Verify(x => x.RefreshAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _ksefClientMock.Verify(x => x.GetInvoiceAsync(It.IsAny<string>(), newToken, It.IsAny<CancellationToken>()), Times.Once);
    }
}