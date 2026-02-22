﻿using System.Reflection;
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

    [Fact]
    public async Task GetInvoiceQrWithKsef_ValidKsefNumber_ReturnsImageContentBlock()
    {
        var token = "valid-token";
        var ksefNum = "1234567890-20231001-123456-12";
        var hash = "test-hash";
        var date = DateTime.UtcNow;
        var vatId = "1111111111";
        var url = "https://ksef.gov.pl/verify";

        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut(vatId);

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
            .Returns(url);

        var result = await sut.GetInvoiceQrWithKsef(ksefNum, cts.Token);

        var contentList = result.ToList();
        Assert.Single(contentList);
        Assert.IsType<ModelContextProtocol.Protocol.ImageContentBlock>(contentList[0]);
        var imageBlock = (ModelContextProtocol.Protocol.ImageContentBlock)contentList[0];
        Assert.NotEmpty(imageBlock.Data);
        Assert.Equal("image/png", imageBlock.MimeType);
    }

    [Fact]
    public async Task GetInvoiceQrWithKsef_MetadataNotFound_ReturnsTextErrorBlock()
    {
        var token = "valid-token";
        var ksefNum = "non-existent-ksef";

        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();

        var pagedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>()
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

        var result = await sut.GetInvoiceQrWithKsef(ksefNum, cts.Token);

        var contentList = result.ToList();
        Assert.Single(contentList);
        Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(contentList[0]);
        var textBlock = (ModelContextProtocol.Protocol.TextContentBlock)contentList[0];
        Assert.Contains(ksefNum, textBlock.Text);
        Assert.Contains("Nie udało się pobrać danych", textBlock.Text);
    }

    [Fact]
    public async Task GetInvoicesListForGivenDate_SameDayRange_ReturnsResults()
    {
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var sameDate = DateTime.UtcNow.Date;

        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await sut.GetInvoicesListForGivenDate(sameDate, sameDate, CancellationToken.None);

        Assert.Same(response, result);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.DateRange.From == sameDate && f.DateRange.To == sameDate),
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceByInvoiceNumber_EmptyInvoiceNumber_QueriesWithEmptyString()
    {
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await sut.GetInvoiceByInvoiceNumber(string.Empty, CancellationToken.None);

        Assert.Same(response, result);
        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.InvoiceNumber == string.Empty),
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceUrl_MultipleInvoicesInMetadata_SelectsCorrectOne()
    {
        var token = "valid-token";
        var vatId = "1111111111";
        var targetKsefNum = "1234567890-20231001-123456-12";
        var targetHash = "target-hash";
        var targetDate = DateTime.UtcNow;

        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut(vatId);

        var pagedResponse = new PagedInvoiceResponse
        {
            Invoices = new List<InvoiceSummary>
            {
                new() { KsefNumber = "other-ksef-1", InvoiceHash = "other-hash-1", InvoicingDate = DateTime.UtcNow.AddDays(-1) },
                new() { KsefNumber = targetKsefNum, InvoiceHash = targetHash, InvoicingDate = targetDate },
                new() { KsefNumber = "other-ksef-2", InvoiceHash = "other-hash-2", InvoicingDate = DateTime.UtcNow.AddDays(-2) }
            }
        };

        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        _verifyLinkMock.Setup(x => x.BuildInvoiceVerificationUrl(vatId, targetDate, targetHash))
            .Returns("https://ksef.gov.pl/correct-link");

        var result = await sut.GetInvoiceUrl(targetKsefNum, CancellationToken.None);

        Assert.Equal("https://ksef.gov.pl/correct-link", result);
        _verifyLinkMock.Verify(x => x.BuildInvoiceVerificationUrl(vatId, targetDate, targetHash), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceByBuyerNip_WithSpecialCharacters_PassesNipAsProvided()
    {
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var nip = "123-456-78-90";
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await sut.GetInvoiceByBuyerNip(nip, CancellationToken.None);

        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.BuyerIdentifier.Value == nip),
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoiceByBuyerVatUe_WithCountryPrefix_PassesVatUeAsProvided()
    {
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var vatUe = "DE123456789";
        var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await sut.GetInvoiceByBuyerVatUe(vatUe, CancellationToken.None);

        _ksefClientMock.Verify(x => x.QueryInvoiceMetadataAsync(
            It.Is<InvoiceQueryFilters>(f => f.BuyerIdentifier.Value == vatUe && f.BuyerIdentifier.Type == BuyerIdentifierType.VatUe),
            token, It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<SortOrder>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TokenRefresh_UpdatesAccessToken_KeepsRefreshToken()
    {
        var oldToken = "old-access-token";
        var refreshToken = "refresh-token";
        var newToken = "new-access-token";
        
        var authResponse = new AuthenticationOperationStatusResponse
        {
            AccessToken = new TokenInfo { Token = oldToken, ValidUntil = DateTime.Now.AddMinutes(1) },
            RefreshToken = new TokenInfo { Token = refreshToken, ValidUntil = DateTime.Now.AddHours(2) }
        };
        
        var field = typeof(KsefTools).GetField("_authenticationResponse", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, authResponse);

        var sut = CreateSut();

        _ksefClientMock.Setup(x => x.RefreshAccessTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenResponse 
            { 
                AccessToken = new TokenInfo { Token = newToken, ValidUntil = DateTime.Now.AddHours(1) } 
            });

        _ksefClientMock.Setup(x => x.GetInvoiceAsync(It.IsAny<string>(), newToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync("xml");

        await sut.GetInvoice("123", CancellationToken.None);

        _ksefClientMock.Verify(x => x.RefreshAccessTokenAsync(refreshToken, It.IsAny<CancellationToken>()), Times.Once);
        _ksefClientMock.Verify(x => x.GetInvoiceAsync("123", newToken, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TokenStillValid_DoesNotRefresh_UsesExistingToken()
    {
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.Now.AddHours(1));
        
        var sut = CreateSut();

        _ksefClientMock.Setup(x => x.GetInvoiceAsync(It.IsAny<string>(), token, It.IsAny<CancellationToken>()))
            .ReturnsAsync("xml");

        await sut.GetInvoice("123", CancellationToken.None);

        _ksefClientMock.Verify(x => x.RefreshAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _ksefClientMock.Verify(x => x.GetInvoiceAsync("123", token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInvoicesListForGivenDate_SetsCorrectDateType()
    {
        var token = "valid-token";
        SetStaticAuthResponse(token, DateTime.UtcNow.AddDays(1));
        var sut = CreateSut();
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        InvoiceQueryFilters? capturedFilters = null;
        _ksefClientMock.Setup(c => c.QueryInvoiceMetadataAsync(
                It.IsAny<InvoiceQueryFilters>(),
                token,
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<SortOrder>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => capturedFilters = req)
            .ReturnsAsync(new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() });

        await sut.GetInvoicesListForGivenDate(from, to, CancellationToken.None);

        Assert.NotNull(capturedFilters);
        Assert.NotNull(capturedFilters.DateRange);
        Assert.Equal(DateType.Issue, capturedFilters.DateRange.DateType);
        Assert.Equal(InvoiceSubjectType.Subject1, capturedFilters.SubjectType);
    }
}