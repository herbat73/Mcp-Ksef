using Authentication.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Authentication.Domain.Services;

/// <summary>
/// Cryptographic utility domain service following Microsoft DDD patterns.
/// Consolidates all cryptographic operations to eliminate code duplication.
/// NO dependencies on infrastructure layer - pure domain service.
/// </summary>
public class CryptographicUtilityService : ICryptographicUtilityService
{
    private readonly ILogger<CryptographicUtilityService> _logger;

    public CryptographicUtilityService(ILogger<CryptographicUtilityService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates RSA signing key with enterprise security standards.
    /// Consolidates duplicate implementations from TokenService, SigningKeyService, MultiTenantTokenService.
    /// </summary>
    public SecurityKey CreateRSASigningKey(int keySize = 2048, string? keyId = null)
    {
        try
        {
            // Use explicit variables for better readability (CLAUDE.md requirement)
            var rsa = RSA.Create(keySize);
            var keyIdValue = keyId ?? "enterprise-signing-key";
            
            // Use RSA instance directly without export/import cycle to maintain key consistency
            var rsaSecurityKey = new RsaSecurityKey(rsa)
            {
                KeyId = keyIdValue
            };

            _logger.LogDebug("Created RSA signing key with size {KeySize} and ID {KeyId}", 
                keySize, keyIdValue);

            return rsaSecurityKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RSA signing key with size {KeySize}", keySize);
            throw;
        }
    }

    /// <summary>
    /// Generates PKCE code verifier following OAuth 2.1 specification.
    /// Consolidates duplicate implementation from AuthenticationTools.
    /// </summary>
    public string GenerateCodeVerifier()
    {
        try
        {
            // Use explicit variable for random bytes (CLAUDE.md requirement)
            var randomBytes = GenerateSecureRandomBytes(32);
            var codeVerifier = ToBase64Url(randomBytes);

            _logger.LogDebug("Generated PKCE code verifier of length {Length}", codeVerifier.Length);

            return codeVerifier;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PKCE code verifier");
            throw;
        }
    }

    /// <summary>
    /// Generates PKCE code challenge from verifier using SHA256.
    /// Consolidates duplicate implementation from AuthenticationTools.
    /// </summary>
    public string GenerateCodeChallenge(string codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
        {
            throw new ArgumentException("Code verifier cannot be null or empty", nameof(codeVerifier));
        }

        try
        {
            // Use explicit variables for SHA256 operations (CLAUDE.md requirement)
            using var sha256 = SHA256.Create();
            var verifierBytes = Encoding.UTF8.GetBytes(codeVerifier);
            var challengeBytes = sha256.ComputeHash(verifierBytes);
            var codeChallenge = ToBase64Url(challengeBytes);

            _logger.LogDebug("Generated PKCE code challenge from verifier");

            return codeChallenge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PKCE code challenge");
            throw;
        }
    }

    /// <summary>
    /// Validates PKCE code verifier against challenge.
    /// Consolidates duplicate implementation from OAuthImplementation.
    /// </summary>
    public bool ValidatePKCE(string codeChallenge, string codeVerifier)
    {
        if (string.IsNullOrEmpty(codeChallenge) || string.IsNullOrEmpty(codeVerifier))
        {
            _logger.LogWarning("PKCE validation failed: challenge or verifier is null/empty");
            return false;
        }

        try
        {
            // Generate challenge from verifier and compare (explicit variables)
            var computedChallenge = GenerateCodeChallenge(codeVerifier);
            var isValid = string.Equals(computedChallenge, codeChallenge, StringComparison.Ordinal);

            if (!isValid)
            {
                _logger.LogWarning("PKCE validation failed: computed challenge does not match provided challenge");
            }
            else
            {
                _logger.LogDebug("PKCE validation successful");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PKCE validation");
            return false;
        }
    }

    /// <summary>
    /// Converts byte array to Base64URL encoding.
    /// Consolidates duplicate Base64URL encoding patterns across multiple files.
    /// </summary>
    public string ToBase64Url(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            throw new ArgumentException("Bytes cannot be null or empty", nameof(bytes));
        }

        try
        {
            // Use explicit variable for base64 conversion (CLAUDE.md requirement)
            var base64String = Convert.ToBase64String(bytes);
            var base64UrlString = base64String
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return base64UrlString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert bytes to Base64URL");
            throw;
        }
    }

    /// <summary>
    /// Converts Base64URL string to byte array.
    /// Consolidates reverse operation for Base64URL decoding.
    /// </summary>
    public byte[] FromBase64Url(string base64Url)
    {
        if (string.IsNullOrEmpty(base64Url))
        {
            throw new ArgumentException("Base64URL string cannot be null or empty", nameof(base64Url));
        }

        try
        {
            // Use explicit variables for URL-safe to standard Base64 conversion
            var base64String = base64Url
                .Replace('-', '+')
                .Replace('_', '/');

            // Add padding if necessary
            var paddingLength = (4 - base64String.Length % 4) % 4;
            if (paddingLength > 0)
            {
                base64String += new string('=', paddingLength);
            }

            var decodedBytes = Convert.FromBase64String(base64String);

            return decodedBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert Base64URL to bytes");
            throw;
        }
    }

    /// <summary>
    /// Generates cryptographically secure random bytes.
    /// Centralized secure random generation for all authentication operations.
    /// </summary>
    public byte[] GenerateSecureRandomBytes(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentException("Length must be positive", nameof(length));
        }

        try
        {
            // Use explicit variable for random number generator (CLAUDE.md requirement)
            var randomBytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            _logger.LogDebug("Generated {Length} secure random bytes", length);

            return randomBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate {Length} secure random bytes", length);
            throw;
        }
    }

    /// <summary>
    /// Generates secure authorization code for OAuth flows.
    /// Consolidates duplicate implementation from OAuthImplementation.
    /// </summary>
    public string GenerateAuthorizationCode()
    {
        try
        {
            // Use explicit variable for authorization code generation
            var randomBytes = GenerateSecureRandomBytes(32);
            var authorizationCode = ToBase64Url(randomBytes);

            _logger.LogDebug("Generated authorization code of length {Length}", authorizationCode.Length);

            return authorizationCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate authorization code");
            throw;
        }
    }
}