using Microsoft.IdentityModel.Tokens;

namespace Authentication.Interfaces;

/// <summary>
/// Interface for managing JWT signing keys following SOLID principles.
/// Single Responsibility: Key management and distribution.
/// </summary>
public interface ISigningKeyService
{
    /// <summary>
    /// Gets the current signing key for JWT token creation and validation.
    /// </summary>
    /// <returns>Security key for JWT operations</returns>
    SecurityKey GetSigningKey();
    
    /// <summary>
    /// Gets signing credentials for JWT token creation.
    /// </summary>
    /// <returns>Signing credentials with algorithm</returns>
    SigningCredentials GetSigningCredentials();
    
    /// <summary>
    /// Gets token validation parameters for JWT Bearer middleware.
    /// </summary>
    /// <param name="issuer">Expected token issuer</param>
    /// <param name="audience">Expected token audience</param>
    /// <returns>Validation parameters for JWT Bearer</returns>
    TokenValidationParameters GetValidationParameters(string issuer, string audience);
}