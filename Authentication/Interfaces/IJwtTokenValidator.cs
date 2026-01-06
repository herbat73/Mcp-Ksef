using System.Security.Claims;

namespace Authentication.Interfaces;

/// <summary>
/// Interface for RFC 9068 compliant JWT access token validation.
/// Single Responsibility: JWT token validation following OAuth 2.1 standards.
/// </summary>
public interface IJwtTokenValidator
{
    /// <summary>
    /// Validates JWT access token according to RFC 9068 specification.
    /// </summary>
    /// <param name="token">JWT access token to validate</param>
    /// <returns>Claims principal if valid, null if invalid</returns>
    Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token);
    
    /// <summary>
    /// Validates JWT header includes required RFC 9068 type.
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <returns>True if header is compliant</returns>
    Task<bool> ValidateJwtHeaderAsync(string token);
    
    /// <summary>
    /// Validates all required JWT claims are present.
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <returns>True if all required claims present</returns>
    Task<bool> ValidateRequiredClaimsAsync(string token);
}

/// <summary>
/// Interface for creating RFC 9068 compliant JWT access tokens.
/// Single Responsibility: JWT token creation following OAuth 2.1 standards.
/// </summary>
public interface IJwtTokenCreator
{
    /// <summary>
    /// Creates RFC 9068 compliant JWT access token.
    /// </summary>
    /// <param name="user">User claims</param>
    /// <param name="clientId">OAuth client ID</param>
    /// <param name="scopes">Requested scopes</param>
    /// <param name="audience">Token audience</param>
    /// <returns>RFC 9068 compliant JWT</returns>
    Task<string> CreateAccessTokenAsync(ClaimsPrincipal user, string clientId, string[] scopes, string audience);
    
    /// <summary>
    /// Creates refresh token with proper token family tracking.
    /// </summary>
    /// <param name="user">User claims</param>
    /// <param name="clientId">OAuth client ID</param>
    /// <param name="tokenFamily">Token family ID for revocation tracking</param>
    /// <returns>Refresh token</returns>
    Task<string> CreateRefreshTokenAsync(ClaimsPrincipal user, string clientId, string tokenFamily);
}

/// <summary>
/// Interface for OAuth 2.1 PKCE validation.
/// Single Responsibility: PKCE challenge/verifier validation.
/// </summary>
public interface IPkceValidator
{
    /// <summary>
    /// Validates PKCE code verifier against stored challenge.
    /// </summary>
    /// <param name="codeVerifier">Code verifier from client</param>
    /// <param name="codeChallenge">Stored code challenge</param>
    /// <param name="challengeMethod">Challenge method (S256)</param>
    /// <returns>True if PKCE validation succeeds</returns>
    Task<bool> ValidatePkceAsync(string codeVerifier, string codeChallenge, string challengeMethod);
    
    /// <summary>
    /// Generates secure PKCE challenge for authorization requests.
    /// </summary>
    /// <returns>Code challenge and verifier pair</returns>
    Task<(string Challenge, string Verifier)> GeneratePkceChallenge();
    
    /// <summary>
    /// Validates that PKCE is present for OAuth 2.1 compliance.
    /// </summary>
    /// <param name="authorizationRequest">Authorization request</param>
    /// <returns>True if PKCE is present and valid</returns>
    Task<bool> ValidatePkcePresenceAsync(Dictionary<string, string> authorizationRequest);
}

/// <summary>
/// Interface for WebAuthn W3C compliant validation.
/// Single Responsibility: WebAuthn credential validation following W3C standards.
/// </summary>
public interface IWebAuthnValidator
{
    /// <summary>
    /// Performs complete W3C 19-point validation procedure.
    /// </summary>
    /// <param name="credentialResponse">WebAuthn credential response</param>
    /// <param name="storedChallenge">Server-stored challenge</param>
    /// <param name="origin">Expected origin</param>
    /// <returns>Validation result with detailed information</returns>
    Task<WebAuthnValidationResult> ValidateCredentialAsync(
        object credentialResponse, 
        string storedChallenge, 
        string origin);
    
    /// <summary>
    /// Validates WebAuthn challenge to prevent replay attacks.
    /// </summary>
    /// <param name="clientDataJSON">Client data from authenticator</param>
    /// <param name="expectedChallenge">Expected challenge value</param>
    /// <returns>True if challenge validation succeeds</returns>
    Task<bool> ValidateChallengeAsync(string clientDataJSON, string expectedChallenge);
    
    /// <summary>
    /// Generates cryptographically secure WebAuthn challenge.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="origin">Origin for the challenge</param>
    /// <returns>Secure challenge for WebAuthn ceremony</returns>
    Task<string> GenerateSecureChallengeAsync(string userId, string origin);
}

/// <summary>
/// Result of WebAuthn validation procedure.
/// </summary>
public class WebAuthnValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> ValidationSteps { get; set; } = new();
    public bool UserVerified { get; set; }
    public string? CredentialId { get; set; }
    public byte[]? PublicKey { get; set; }
    public uint SignatureCounter { get; set; }
}