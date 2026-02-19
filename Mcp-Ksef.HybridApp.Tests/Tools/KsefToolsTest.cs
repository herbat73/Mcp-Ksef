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
        public async Task GetInvoice_ForwardsArguments_AndReturnsInvoice()
        {
            try
            {
                // Arrange
                const string expectedInvoice = "invoice-xml";
                const string authToken = "auth-token";
                const string ksefNumber = "KSEF-123";
                using var cts = new CancellationTokenSource();

                SetStaticAuthToken(authToken);
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                ksefClient.Setup(c => c.GetInvoiceAsync(
                        ksefNumber,
                        authToken,
                        It.Is<CancellationToken>(ct => ct == cts.Token)))
                    .ReturnsAsync(expectedInvoice)
                    .Verifiable();

                // Act
                var result = await sut.GetInvoice(ksefNumber, cts.Token);

                // Assert
                Assert.Equal(expectedInvoice, result);
                ksefClient.Verify();
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        [Fact]
        public async Task GetInvoicesListForGivenDate_BuildsExpectedFilter_AndForwardsCancellationToken()
        {
            try
            {
                // Arrange
                var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var to = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc);
                var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
                using var cts = new CancellationTokenSource();

                SetStaticAuthToken("auth-token");
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                InvoiceQueryFilters? captured = null;
                ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                        It.IsAny<InvoiceQueryFilters>(),
                        "auth-token",
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
                Assert.NotNull(captured);
                Assert.Equal(InvoiceSubjectType.Subject1, captured.SubjectType);
                Assert.NotNull(captured.DateRange);
                Assert.Equal(DateType.Issue, captured.DateRange.DateType);
                Assert.Equal(from, captured.DateRange.From);
                Assert.Equal(to, captured.DateRange.To);
                ksefClient.Verify();
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        [Fact]
        public async Task GetInvoiceByInvoiceNumber_SetsInvoiceNumber_AndUsesIssueDateRange()
        {
            try
            {
                // Arrange
                const string invoiceNumber = "INV-2025-001";
                var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

                SetStaticAuthToken("auth-token");
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                InvoiceQueryFilters? captured = null;
                ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                        It.IsAny<InvoiceQueryFilters>(),
                        "auth-token",
                        It.IsAny<int?>(),
                        It.IsAny<int?>(),
                        It.IsAny<SortOrder>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
                    .ReturnsAsync(response)
                    .Verifiable();

                // Act
                var result = await sut.GetInvoiceByInvoiceNumber(invoiceNumber, CancellationToken.None);

                // Assert
                Assert.Same(response, result);
                Assert.NotNull(captured);
                Assert.Equal(invoiceNumber, captured.InvoiceNumber);
                Assert.NotNull(captured.DateRange);
                Assert.Equal(DateType.Issue, captured.DateRange.DateType);
                Assert.True(captured.DateRange.To >= captured.DateRange.From);
                ksefClient.Verify();
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        [Fact]
        public async Task GetInvoiceByBuyerNip_SetsBuyerIdentifierNip()
        {
            try
            {
                // Arrange
                const string nip = "PL1234567890";
                var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

                SetStaticAuthToken("auth-token");
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                InvoiceQueryFilters? captured = null;
                ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                        It.IsAny<InvoiceQueryFilters>(),
                        "auth-token",
                        It.IsAny<int?>(),
                        It.IsAny<int?>(),
                        It.IsAny<SortOrder>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
                    .ReturnsAsync(response)
                    .Verifiable();

                // Act
                var result = await sut.GetInvoiceByBuyerNip(nip, CancellationToken.None);

                // Assert
                Assert.Same(response, result);
                Assert.NotNull(captured);
                Assert.NotNull(captured.BuyerIdentifier);
                Assert.Equal(BuyerIdentifierType.Nip, captured.BuyerIdentifier.Type);
                Assert.Equal(nip, captured.BuyerIdentifier.Value);
                Assert.Equal(DateType.Issue, captured.DateRange.DateType);
                ksefClient.Verify();
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        [Fact]
        public async Task GetInvoiceByBuyerVatUe_SetsBuyerIdentifierVatUe()
        {
            try
            {
                // Arrange
                const string vatUe = "EU123456789";
                var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };

                SetStaticAuthToken("auth-token");
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                InvoiceQueryFilters? captured = null;
                ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                        It.IsAny<InvoiceQueryFilters>(),
                        "auth-token",
                        It.IsAny<int?>(),
                        It.IsAny<int?>(),
                        It.IsAny<SortOrder>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
                    .ReturnsAsync(response)
                    .Verifiable();

                // Act
                var result = await sut.GetInvoiceByBuyerVatUe(vatUe, CancellationToken.None);

                // Assert
                Assert.Same(response, result);
                Assert.NotNull(captured);
                Assert.NotNull(captured.BuyerIdentifier);
                Assert.Equal(BuyerIdentifierType.VatUe, captured.BuyerIdentifier.Type);
                Assert.Equal(vatUe, captured.BuyerIdentifier.Value);
                Assert.Equal(DateType.Issue, captured.DateRange.DateType);
                ksefClient.Verify();
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        [Fact]
        public async Task GetInvoiceUrl_UsesMetadataAndVerificationService_ReturnsBuiltUrl()
        {
            try
            {
                // Arrange
                const string ksefNumber = "KSEF-ABC-1";
                const string invoiceHash = "HASH123";
                var invoicingDate = DateTimeOffset.UtcNow;
                var expectedUrl = "https://ksef.example/invoice/view/123";

                var metadata = new PagedInvoiceResponse
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

                SetStaticAuthToken("auth-token");
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                InvoiceQueryFilters? captured = null;
                ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                        It.IsAny<InvoiceQueryFilters>(),
                        "auth-token",
                        It.IsAny<int?>(),
                        It.IsAny<int?>(),
                        It.IsAny<SortOrder>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken>((req, _, _, _, _, _) => captured = req)
                    .ReturnsAsync(metadata)
                    .Verifiable();

                verification.Setup(v => v.BuildInvoiceVerificationUrl(EnvVatId, invoicingDate.DateTime, invoiceHash))
                    .Returns(expectedUrl)
                    .Verifiable();

                // Act
                var result = await sut.GetInvoiceUrl(ksefNumber, CancellationToken.None);

                // Assert
                Assert.Equal(expectedUrl, result);
                Assert.NotNull(captured);
                Assert.Equal(ksefNumber, captured.KsefNumber);
                verification.Verify();
                ksefClient.Verify();
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        [Fact]
        public async Task GetInvoiceUrl_WhenNoMatchingKsefNumber_ThrowsInvalidOperationException()
        {
            try
            {
                // Arrange
                const string requestedKsef = "KSEF-NOT-FOUND";
                var metadata = new PagedInvoiceResponse
                {
                    Invoices = new List<InvoiceSummary>
                    {
                        new InvoiceSummary
                        {
                            KsefNumber = "KSEF-OTHER",
                            InvoiceHash = "HASH-X",
                            InvoicingDate = DateTimeOffset.UtcNow
                        }
                    }
                };

                SetStaticAuthToken("auth-token");
                var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);

                ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
                        It.IsAny<InvoiceQueryFilters>(),
                        "auth-token",
                        It.IsAny<int?>(),
                        It.IsAny<int?>(),
                        It.IsAny<SortOrder>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(metadata);

                // Act + Assert
                await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetInvoiceUrl(requestedKsef, CancellationToken.None));
            }
            finally
            {
                ClearEnvVars();
                SetStaticAuthToken(null);
            }
        }

        // [Fact]
        // public async Task GetInvoice_ReturnsInvoiceString_UsesAuthToken()
        // {
        //     // Arrange
        //     var expectedInvoice = "invoice-payload";
        //     SetStaticAuthToken("auth-token");
        //     var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);
        //
        //     ksefClient.Setup(c => c.GetInvoiceAsync(
        //             "ksef-123", 
        //             "auth-token", 
        //             It.IsAny<CancellationToken>()))
        //               .ReturnsAsync(expectedInvoice)
        //               .Verifiable();
        //
        //     // Act
        //     var result = await sut.GetInvoice("ksef-123");
        //
        //     // Assert
        //     Assert.Equal(expectedInvoice, result);
        //     ksefClient.Verify();
        //     ClearEnvVars();
        //     SetStaticAuthToken(null);
        // }
        //
        // [Fact]
        // public async Task GetInvoicesListForGivenDate_CallsQueryWithDateRange_ReturnsPagedInvoiceResponse()
        // {
        //     // Arrange
        //     var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        //     SetStaticAuthToken("auth-token");
        //     var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);
        //
        //     InvoiceQueryFilters? capturedRequest = null;
        //     ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
        //             It.IsAny<InvoiceQueryFilters>(), 
        //             "auth-token", 
        //             It.IsAny<int?>(), 
        //             It.IsAny<int?>(),
        //             It.IsAny<SortOrder>(), 
        //             It.IsAny<CancellationToken>()))
        //         .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
        //         .ReturnsAsync(response)
        //         .Verifiable();
        //
        //     var from = new DateTime(2025, 1, 1);
        //     var to = new DateTime(2025, 1, 31);
        //
        //     // Act
        //     var result = await sut.GetInvoicesListForGivenDate(from, to);
        //
        //     // Assert
        //     Assert.Same(response, result);
        //     Assert.NotNull(capturedRequest);
        //     Assert.Equal(DateType.Issue, capturedRequest.DateRange.DateType);
        //     Assert.Equal(from.Date, capturedRequest.DateRange.From.Date);
        //     Assert.Equal(to.Date, capturedRequest.DateRange.To.Value.Date);
        //     ksefClient.Verify();
        //     ClearEnvVars();
        //     SetStaticAuthToken(null);
        // }
        //
        // [Fact]
        // public async Task GetInvoiceByInvoiceNumber_SetsInvoiceNumberOnQuery_ReturnsPagedInvoiceResponse()
        // {
        //     // Arrange
        //     var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        //     SetStaticAuthToken("auth-token");
        //     var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);
        //
        //     InvoiceQueryFilters? capturedRequest = null;
        //     ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
        //             It.IsAny<InvoiceQueryFilters>(), 
        //             "auth-token", 
        //             It.IsAny<int?>(), 
        //             It.IsAny<int?>(),
        //             It.IsAny<SortOrder>(), 
        //             It.IsAny<CancellationToken>()))
        //         .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
        //         .ReturnsAsync(response)
        //         .Verifiable();
        //
        //     var invoiceNumber = "INV-2025-001";
        //
        //     // Act
        //     var result = await sut.GetInvoiceByInvoiceNumber(invoiceNumber);
        //
        //     // Assert
        //     Assert.Same(response, result);
        //     Assert.NotNull(capturedRequest);
        //     Assert.Equal(invoiceNumber, capturedRequest.InvoiceNumber);
        //     ksefClient.Verify();
        //     ClearEnvVars();
        //     SetStaticAuthToken(null);
        // }
        //
        // [Fact]
        // public async Task GetInvoiceByBuyerNip_SetsBuyerIdentifierNip_ReturnsPagedInvoiceResponse()
        // {
        //     // Arrange
        //     var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        //     SetStaticAuthToken("auth-token");
        //     var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);
        //
        //     InvoiceQueryFilters? capturedRequest = null;
        //     ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
        //             It.IsAny<InvoiceQueryFilters>(), 
        //             "auth-token", 
        //             It.IsAny<int?>(), 
        //             It.IsAny<int?>(),
        //             It.IsAny<SortOrder>(), 
        //             It.IsAny<CancellationToken>()))
        //               .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
        //               .ReturnsAsync(response)
        //               .Verifiable();
        //
        //     var nip = "PL1234567890";
        //
        //     // Act
        //     var result = await sut.GetInvoiceByBuyerNip(nip);
        //
        //     // Assert
        //     Assert.Same(response, result);
        //     Assert.NotNull(capturedRequest);
        //     Assert.NotNull(capturedRequest.BuyerIdentifier);
        //     Assert.Equal(BuyerIdentifierType.Nip, capturedRequest.BuyerIdentifier.Type);
        //     Assert.Equal(nip, capturedRequest.BuyerIdentifier.Value);
        //     ksefClient.Verify();
        //     ClearEnvVars();
        //     SetStaticAuthToken(null);
        // }
        //
        // [Fact]
        // public async Task GetInvoiceByBuyerVatUe_SetsBuyerIdentifierVatUe_ReturnsPagedInvoiceResponse()
        // {
        //     // Arrange
        //     var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary>() };
        //     SetStaticAuthToken("auth-token");
        //     var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);
        //
        //     InvoiceQueryFilters? capturedRequest = null;
        //     ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
        //             It.IsAny<InvoiceQueryFilters>(), 
        //             "auth-token", 
        //             It.IsAny<int?>(), 
        //             It.IsAny<int?>(),
        //             It.IsAny<SortOrder>(), 
        //             It.IsAny<CancellationToken>()))
        //         .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
        //         .ReturnsAsync(response)
        //         .Verifiable();
        //
        //     var vatUe = "EU123456789";
        //
        //     // Act
        //     var result = await sut.GetInvoiceByBuyerVatUe(vatUe);
        //
        //     // Assert
        //     Assert.Same(response, result);
        //     Assert.NotNull(capturedRequest);
        //     Assert.NotNull(capturedRequest.BuyerIdentifier);
        //     Assert.Equal(BuyerIdentifierType.VatUe, capturedRequest.BuyerIdentifier.Type);
        //     Assert.Equal(vatUe, capturedRequest.BuyerIdentifier.Value);
        //     ksefClient.Verify();
        //     ClearEnvVars();
        //     SetStaticAuthToken(null);
        // }
        //
        // [Fact]
        // public async Task GetInvoiceUrl_UsesMetadataAndVerificationService_ReturnsUrl()
        // {
        //     // Arrange
        //     var ksefNumber = "KSEF-ABC-1";
        //     var invoiceHash = "HASH123";
        //     var invoicingDate = DateTimeOffset.UtcNow;
        //     var invoiceMeta = new InvoiceSummary
        //     {
        //         KsefNumber = ksefNumber,
        //         InvoiceHash = invoiceHash,
        //         InvoicingDate = invoicingDate
        //     };
        //     var response = new PagedInvoiceResponse { Invoices = new List<InvoiceSummary> { invoiceMeta } };
        //
        //     SetStaticAuthToken("auth-token");
        //     var sut = CreateSut(out var logger, out var authClient, out var crypto, out var ksefClient, out var verification);
        //
        //     InvoiceQueryFilters? capturedRequest = null;
        //     ksefClient.Setup(c => c.QueryInvoiceMetadataAsync(
        //             It.IsAny<InvoiceQueryFilters>(), 
        //             "auth-token", 
        //             It.IsAny<int?>(), 
        //             It.IsAny<int?>(),
        //             It.IsAny<SortOrder>(), 
        //             It.IsAny<CancellationToken>()))
        //         .Callback<InvoiceQueryFilters, string, int?, int?, SortOrder, CancellationToken >((req, t, pageOffset, pageSize, sortOrder, cancellationToken) => capturedRequest = req)
        //         .ReturnsAsync(response)
        //         .Verifiable();
        //
        //     var expectedUrl = "https://ksef.example/invoice/view/123";
        //     verification.Setup(v => v.BuildInvoiceVerificationUrl(EnvVatId, invoicingDate.DateTime, invoiceHash))
        //                 .Returns(expectedUrl)
        //                 .Verifiable();
        //
        //     // Act
        //     var result = await sut.GetInvoiceUrl(ksefNumber);
        //
        //     // Assert
        //     Assert.Equal(expectedUrl, result);
        //     ksefClient.Verify();
        //     verification.Verify();
        //     ClearEnvVars();
        //     SetStaticAuthToken(null);
        // }
    }
}