using Microsoft.IdentityModel.Tokens;

namespace Authentication.Interfaces;

/// <summary>
/// Cryptographic utility service interface following SOLID principles.
/// Single Responsibility: Manages all cryptographic operations consistently.
/// Dependency Inversion: Provides abstractions for cryptographic utilities.
/// Interface Segregation: Focused on cryptographic operations only.
/// </summary>
public interface ICryptographicUtilityService
{
    /// <summary>
    /// Creates RSA signing key with specified key size for JWT operations.
    /// Consistent key generation across all services.
    /// </summary>
    /// <param name="keySize">RSA key size in bits (default 2048 for enterprise)</param>
    /// <param name="keyId">Optional key identifier for JWT validation</param>
    /// <returns>RSA security key for signing operations</returns>
    SecurityKey CreateRSASigningKey(int keySize = 2048, string? keyId = null);

    /// <summary>
    /// Generates cryptographically secure PKCE code verifier.
    /// Implements OAuth 2.1 PKCE specification (RFC 7636).
    /// </summary>
    /// <returns>Base64URL-encoded code verifier</returns>
    string GenerateCodeVerifier();

    /// <summary>
    /// Generates PKCE code challenge from verifier using SHA256.
    /// Implements OAuth 2.1 PKCE specification (RFC 7636).
    /// </summary>
    /// <param name="codeVerifier">Code verifier to hash</param>
    /// <returns>Base64URL-encoded code challenge</returns>
    string GenerateCodeChallenge(string codeVerifier);

    /// <summary>
    /// Validates PKCE code verifier against challenge.
    /// Implements OAuth 2.1 PKCE specification (RFC 7636).
    /// </summary>
    /// <param name="codeChallenge">Expected code challenge</param>
    /// <param name="codeVerifier">Code verifier to validate</param>
    /// <returns>True if verifier matches challenge</returns>
    bool ValidatePKCE(string codeChallenge, string codeVerifier);

    /// <summary>
    /// Converts byte array to Base64URL encoding.
    /// Used for JWT, OAuth, and WebAuthn specifications.
    /// </summary>
    /// <param name="bytes">Bytes to encode</param>
    /// <returns>Base64URL-encoded string</returns>
    string ToBase64Url(byte[] bytes);

    /// <summary>
    /// Converts Base64URL string to byte array.
    /// Used for JWT, OAuth, and WebAuthn specifications.
    /// </summary>
    /// <param name="base64Url">Base64URL-encoded string</param>
    /// <returns>Decoded byte array</returns>
    byte[] FromBase64Url(string base64Url);

    /// <summary>
    /// Generates cryptographically secure random bytes.
    /// Used for tokens, challenges, and security purposes.
    /// </summary>
    /// <param name="length">Number of random bytes to generate</param>
    /// <returns>Cryptographically secure random bytes</returns>
    byte[] GenerateSecureRandomBytes(int length);

    /// <summary>
    /// Generates secure authorization code for OAuth flows.
    /// Implements OAuth 2.1 specification requirements.
    /// </summary>
    /// <returns>Secure authorization code</returns>
    string GenerateAuthorizationCode();
}