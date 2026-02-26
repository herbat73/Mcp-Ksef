using System.Security.Cryptography.X509Certificates;
using KSeF.Client.Api.Builders.Auth;
using KSeF.Client.Api.Services;
using KSeF.Client.Core.Interfaces.Clients;
using KSeF.Client.Core.Interfaces.Services;
using KSeF.Client.Core.Models;
using KSeF.Client.Core.Models.Authorization;
using McpKsef.HybridApp.Helpers;
using Shared.Consts;

namespace McpKsef.HybridApp.Services;

public class KsefAuthorizationService : IKsefAuthorizationService
{
    private readonly ILogger<KsefAuthorizationService> _logger;
    private readonly string? _vatId;
    private readonly AuthenticationResponse _authenticationResponse;
    private readonly IAuthorizationClient _authorizationClient;
    private readonly ICryptographyService _cryptographyService;
    private readonly IKSeFClient _ksefClient;
    
    public KsefAuthorizationService(ILogger<KsefAuthorizationService> logger, IAuthorizationClient authorizationClient, ICryptographyService cryptographyService, IKSeFClient ksefClient, AuthenticationResponse authenticationResponse)
    {
        _logger = logger;
        _vatId = Environment.GetEnvironmentVariable(EnvironmentConsts.VatId);
        _authorizationClient =  authorizationClient;
        _cryptographyService =  cryptographyService;
        _ksefClient = ksefClient;
        _authenticationResponse = authenticationResponse;
    }

    public AuthenticationOperationStatusResponse GetAuthenticationInfo()
    {
        if (_authenticationResponse.Response is null)
        {
            _logger.LogError("AuthenticationOperationStatusResponse is null");
            throw new InvalidOperationException();
        }
        
        return _authenticationResponse.Response;
    }
    
    public async Task VerifyAuthToken(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(VerifyAuthToken)} called");
        
        if (_authenticationResponse.Response == null)
        {
            _logger.LogInformation("Pobieranie tokena.");
            
            var infoHelperResult = RunInfoHelper.CheckEnvironmentConsts();
            _authenticationResponse.Response = infoHelperResult.IsKsefCertificateValid
                ? await GetAccessTokenByCertAsync(_vatId, cancellationToken)
                : await GetAccessTokenFromKsefTokenAsync(_vatId, cancellationToken);
        }
        else
        {
            var refreshTime = DateTime.Now.AddMinutes(5);
            if (_authenticationResponse.Response?.AccessToken.ValidUntil < refreshTime)
            {
                _logger.LogInformation(
                    $"Refreshing token as it is valid until: {_authenticationResponse.Response?.AccessToken.ValidUntil}");

                var tokenRefreshResponse =
                    await _ksefClient.RefreshAccessTokenAsync(_authenticationResponse.Response!.RefreshToken.Token,
                        cancellationToken);
                _authenticationResponse.Response.AccessToken = tokenRefreshResponse.AccessToken;
                _logger.LogInformation(
                    $"Token refreshed, now it valid until: {_authenticationResponse.Response?.AccessToken.ValidUntil}");
            }
        }
    }
    
    private async Task<AuthenticationOperationStatusResponse> GetAccessTokenFromKsefTokenAsync(string nip, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{nameof(GetAccessTokenFromKsefTokenAsync)} called nip: {nip}");
        
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
        _logger.LogInformation($"{nameof(GetAccessTokenByCertAsync)} called nip: {nip}");
        
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
            .SubmitXadesAuthRequestAsync(signedXml, false, false, cancellationToken);

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
}