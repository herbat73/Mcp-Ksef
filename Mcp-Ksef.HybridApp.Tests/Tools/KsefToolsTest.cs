using System.Reflection;
using KSeF.Client.Core.Models.Invoices;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Consts;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;

namespace McpKsef.HybridApp.Tools.Tests
{
    public class KsefToolsTest
    {
        private const string EnvKsefToken = "TEST_KSEF_TOKEN";
        private const string EnvVatId = "TEST_VAT_ID";

        private static void SetEnvVars()
        {
            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, EnvKsefToken);
            Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, EnvVatId);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, null);
            Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, null);
        }

        private static void SetStaticAuthToken(string? token)
        {
            var field = typeof(KsefTools).GetField("_authToken", BindingFlags.Static | BindingFlags.NonPublic);
            field!.SetValue(null, token);
        }

        private static KsefTools CreateSut(
            out Mock<ILogger<KsefTools>> loggerMock,
            out Mock<IAuthorizationClient> authClientMock,
            out Mock<ICryptographyService> cryptoMock,
            out Mock<IKSeFClient> ksefClientMock,
            out Mock<IVerificationLinkService> verificationMock)
        {
            SetEnvVars();
            loggerMock = new Mock<ILogger<KsefTools>>();
            authClientMock = new Mock<IAuthorizationClient>();
            cryptoMock = new Mock<ICryptographyService>();
            ksefClientMock = new Mock<IKSeFClient>();
            verificationMock = new Mock<IVerificationLinkService>();

            var sut = new KsefTools(
                loggerMock.Object,
                authClientMock.Object,
                cryptoMock.Object,
                ksefClientMock.Object,
                verificationMock.Object
            );

            return sut;
        }

        [Fact]
        public async Task GetInvoice_ReturnsInvoiceString_UsesAuthToken()
        {
            // Arrange
            var expectedInvoice = "invoice-payload";
            SetStaticAuthToken("auth-token");
            var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

            ksefClient.Setup(c => c.GetInvoiceAsync(
                    "ksef-123", 
                    "auth-token", 
                    It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedInvoice)
                      .Verifiable();

            // Act
            var result = await sut.GetInvoice("ksef-123");

            // Assert
            Assert.Equal(expectedInvoice, result);
            ksefClient.Verify();
            ClearEnvVars();
            SetStaticAuthToken(null);
        }

        [Fact]
        public async Task GetInvoicesListForGivenDate_CallsQueryWithDateRange_ReturnsPagedInvoiceResponse()
        {
            // Arrange
            var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
            SetStaticAuthToken("auth-token");
            var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

            InvoiceQueryFilters? capturedRequest = null;
            ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                    It.IsAny<InvoiceQueryFilters>(), 
                    "auth-token", 
                    It.IsAny<int?>(), 
                    It.IsAny<int?>(),
                    It.IsAny<SortOrder>(), 
                    It.IsAny<CancellationToken>()))
                .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
                .ReturnsAsync(response)
                .Verifiable();

            var from = new DateTime(2025, 1, 1);
            var to = new DateTime(2025, 1, 31);

            // Act
            var result = await sut.GetInvoicesListForGivenDate(from, to);

            // Assert
            Assert.Same(response, result);
            Assert.NotNull(capturedRequest);
            Assert.Equal(DateType.Issue, capturedRequest.DateRange.DateType);
            Assert.Equal(from.Date, capturedRequest.DateRange.From.Date);
            Assert.Equal(to.Date, capturedRequest.DateRange.To.Value.Date);
            ksefClient.Verify();
            ClearEnvVars();
            SetStaticAuthToken(null);
        }

        [Fact]
        public async Task GetInvoiceByInvoiceNumber_SetsInvoiceNumberOnQuery_ReturnsPagedInvoiceResponse()
        {
            // Arrange
            var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
            SetStaticAuthToken("auth-token");
            var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

            InvoiceQueryFilters? capturedRequest = null;
            ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                    It.IsAny<InvoiceQueryFilters>(), 
                    "auth-token", 
                    It.IsAny<int?>(), 
                    It.IsAny<int?>(),
                    It.IsAny<SortOrder>(), 
                    It.IsAny<CancellationToken>()))
                .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
                .ReturnsAsync(response)
                .Verifiable();

            var invoiceNumber = "INV-2025-001";

            // Act
            var result = await sut.GetInvoiceByInvoiceNumber(invoiceNumber);

            // Assert
            Assert.Same(response, result);
            Assert.NotNull(capturedRequest);
            Assert.Equal(invoiceNumber, capturedRequest.InvoiceNumber);
            ksefClient.Verify();
            ClearEnvVars();
            SetStaticAuthToken(null);
        }

        [Fact]
        public async Task GetInvoiceByBuyerNip_SetsBuyerIdentifierNip_ReturnsPagedInvoiceResponse()
        {
            // Arrange
            var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
            SetStaticAuthToken("auth-token");
            var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

            InvoiceQueryFilters? capturedRequest = null;
            ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                    It.IsAny<InvoiceQueryFilters>(), 
                    "auth-token", 
                    It.IsAny<int?>(), 
                    It.IsAny<int?>(),
                    It.IsAny<SortOrder>(), 
                    It.IsAny<CancellationToken>()))
                      .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
                      .ReturnsAsync(response)
                      .Verifiable();

            var nip = "PL1234567890";

            // Act
            var result = await sut.GetInvoiceByBuyerNip(nip);

            // Assert
            Assert.Same(response, result);
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.BuyerIdentifier);
            Assert.Equal(BuyerIdentifierType.Nip, capturedRequest.BuyerIdentifier.Type);
            Assert.Equal(nip, capturedRequest.BuyerIdentifier.Value);
            ksefClient.Verify();
            ClearEnvVars();
            SetStaticAuthToken(null);
        }

        [Fact]
        public async Task GetInvoiceByBuyerVatUe_SetsBuyerIdentifierVatUe_ReturnsPagedInvoiceResponse()
        {
            // Arrange
            var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
            SetStaticAuthToken("auth-token");
            var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

            InvoiceQueryFilters? capturedRequest = null;
            ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                    It.IsAny<InvoiceQueryFilters>(), 
                    "auth-token", 
                    It.IsAny<int?>(), 
                    It.IsAny<int?>(),
                    It.IsAny<SortOrder>(), 
                    It.IsAny<CancellationToken>()))
                .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
                .ReturnsAsync(response)
                .Verifiable();

            var vatUe = "EU123456789";

            // Act
            var result = await sut.GetInvoiceByBuyerVatUe(vatUe);

            // Assert
            Assert.Same(response, result);
            Assert.NotNull(capturedRequest);
            Assert.NotNull(capturedRequest.BuyerIdentifier);
            Assert.Equal(BuyerIdentifierType.VatUe, capturedRequest.BuyerIdentifier.Type);
            Assert.Equal(vatUe, capturedRequest.BuyerIdentifier.Value);
            ksefClient.Verify();
            ClearEnvVars();
            SetStaticAuthToken(null);
        }

        [Fact]
        public async Task GetInvoiceUrl_UsesMetadataAndVerificationService_ReturnsUrl()
        {
            // Arrange
            var ksefNumber = "KSEF-ABC-1";
            var invoiceHash = "HASH123";
            var invoicingDate = DateTimeOffset.UtcNow;
            var invoiceMeta = new InvoiceSummary
            {
                KsefNumber = ksefNumber,
                InvoiceHash = invoiceHash,
                InvoicingDate = invoicingDate
            };
            var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary> { invoiceMeta } };

            SetStaticAuthToken("auth-token");
            var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

            InvoiceQueryFilters? capturedRequest = null;
            ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                    It.IsAny<InvoiceQueryFilters>(), 
                    "auth-token", 
                    It.IsAny<int?>(), 
                    It.IsAny<int?>(),
                    It.IsAny<SortOrder>(), 
                    It.IsAny<CancellationToken>()))
                .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
                .ReturnsAsync(response)
                .Verifiable();

            var expectedUrl = "https://ksef.example/invoice/view/123";
            verification.Setup(v => v.BuildInvoiceVerificationUrl(EnvVatId, invoicingDate.DateTime, invoiceHash))
                        .Returns(expectedUrl)
                        .Verifiable();

            // Act
            var result = await sut.GetInvoiceUrl(ksefNumber);

            // Assert
            Assert.Equal(expectedUrl, result);
            ksefClient.Verify();
            verification.Verify();
            ClearEnvVars();
            SetStaticAuthToken(null);
        }
    }
}