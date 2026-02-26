using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models.Authorization;
using McpKsef.HybridApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Consts;

namespace McpKsef.HybridApp.Tests.Services;

public class KsefAuthorizationServiceTest : IDisposable
{
    private readonly Mock<ILogger<KsefAuthorizationService>> _loggerMock;
    private readonly Mock<IAuthorizationClient> _authorizationClientMock;
    private readonly Mock<ICryptographyService> _cryptographyServiceMock;
    private readonly Mock<IKSeFClient> _ksefClientMock;
    private readonly AuthenticationResponse _authenticationResponse;
    private string _originalVatId;
    private string _originalKsefToken;
    private string _originalCertFile;
    private string _originalKeyFile;
    private string _originalPassword;

    public KsefAuthorizationServiceTest()
    {
        _loggerMock = new Mock<ILogger<KsefAuthorizationService>>();
        _authorizationClientMock = new Mock<IAuthorizationClient>();
        _cryptographyServiceMock = new Mock<ICryptographyService>();
        _ksefClientMock = new Mock<IKSeFClient>();
        _authenticationResponse = new AuthenticationResponse();

        _originalVatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        _originalKsefToken = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefToken);
        _originalCertFile = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile);
        _originalKeyFile = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile);
        _originalPassword = Environment.GetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentConsts.VatId, _originalVatId);
        Environment.SetEnvironmentVariable(EnvironmentConsts.KsefToken, _originalKsefToken);
        Environment.SetEnvironmentVariable(EnvironmentConsts.KsefCertificateFile, _originalCertFile);
        Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyFile, _originalKeyFile);
        Environment.SetEnvironmentVariable(EnvironmentConsts.KsefPrivateKeyPassword, _originalPassword);
    }

    [Fact]
    public void GetAuthenticationInfo_WhenResponseIsNull_ThrowsInvalidOperationException()
    {
        var service = new KsefAuthorizationService(
            _loggerMock.Object,
            _authorizationClientMock.Object,
            _cryptographyServiceMock.Object,
            _ksefClientMock.Object,
            _authenticationResponse);

        Assert.Throws<InvalidOperationException>(() => service.GetAuthenticationInfo());
    }

    [Fact]
    public void GetAuthenticationInfo_WhenResponseIsSet_ReturnsResponse()
    {
        var expectedResponse = new AuthenticationOperationStatusResponse
        {
            AccessToken = new TokenInfo{ Token = "test-token", ValidUntil = DateTime.UtcNow.AddHours(1) }
        };
        _authenticationResponse.Response = expectedResponse;

        var service = new KsefAuthorizationService(
            _loggerMock.Object,
            _authorizationClientMock.Object,
            _cryptographyServiceMock.Object,
            _ksefClientMock.Object,
            _authenticationResponse);

        var result = service.GetAuthenticationInfo();

        Assert.Same(expectedResponse, result);
    }

    [Fact]
    public async Task VerifyAuthToken_WhenTokenStillValid_DoesNotRefresh()
    {
        var validTime = DateTime.Now.AddHours(1);
        _authenticationResponse.Response = new AuthenticationOperationStatusResponse
        {
            AccessToken = new TokenInfo{ Token = "valid-token", ValidUntil = validTime },
            RefreshToken = new TokenInfo { Token = "refresh-token" }
        };

        var service = new KsefAuthorizationService(
            _loggerMock.Object,
            _authorizationClientMock.Object,
            _cryptographyServiceMock.Object,
            _ksefClientMock.Object,
            _authenticationResponse);

        await service.VerifyAuthToken(CancellationToken.None);

        Assert.Equal("valid-token", _authenticationResponse.Response.AccessToken.Token);
        _ksefClientMock.Verify(x => x.RefreshAccessTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
