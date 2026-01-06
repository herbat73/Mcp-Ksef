using Authentication.Interfaces;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Authentication.Services;

/// <summary>
/// Enterprise signing key service implementing SOLID principles.
/// Single Responsibility: Manages JWT signing keys consistently.
/// Dependency Inversion: Provides abstractions for key management.
/// Refactored to use CryptographicUtilityService, eliminating code duplication.
/// </summary>
public class SigningKeyService : ISigningKeyService
{
    private readonly ICryptographicUtilityService _cryptographicUtilityService;
    private static SecurityKey? _sharedSigningKey;
    private static readonly object _keyLock = new object();

    public SigningKeyService(ICryptographicUtilityService cryptographicUtilityService)
    {
        _cryptographicUtilityService = cryptographicUtilityService;
    }

    /// <summary>
    /// Gets the current signing key for JWT token creation and validation.
    /// Thread-safe singleton pattern ensures consistent key across all services.
    /// </summary>
    public SecurityKey GetSigningKey()
    {
        if (_sharedSigningKey == null)
        {
            lock (_keyLock)
            {
                if (_sharedSigningKey == null)
                {
                    _sharedSigningKey = _cryptographicUtilityService.CreateRSASigningKey(2048, "enterprise-signing-key");
                }
            }
        }
        return _sharedSigningKey;
    }

    /// <summary>
    /// Gets signing credentials for JWT token creation.
    /// </summary>
    public SigningCredentials GetSigningCredentials()
    {
        return new SigningCredentials(GetSigningKey(), SecurityAlgorithms.RsaSha256);
    }

    /// <summary>
    /// Gets token validation parameters for JWT Bearer middleware.
    /// Ensures same key is used for creation and validation.
    /// </summary>
    public TokenValidationParameters GetValidationParameters(string issuer, string audience)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSigningKey(), // Same key used for creation
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireSignedTokens = true,
            RequireExpirationTime = true
        };
    }

}