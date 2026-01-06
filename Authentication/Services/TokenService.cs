using Authentication.Configuration;
using Authentication.Domain.Services;
using Authentication.Domain.Entities;
using Authentication.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Authentication.Services;

/// <summary>
/// Enterprise token service for OAuth 2.1 JWT token management.
/// Implements secure token creation, validation, and lifecycle management.
/// Refactored to use Domain Service following DDD and SOLID principles.
/// </summary>
public interface ITokenService
{
    Task<string> CreateAccessTokenAsync(ClaimsPrincipal user, string clientId, string[] scopes, string? tenantId = null);
    Task<string> CreateRefreshTokenAsync(ClaimsPrincipal user, string clientId, string? tenantId = null);
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
    Task<bool> RevokeTokenAsync(string tokenId);
    Task<bool> IsTokenRevokedAsync(string tokenId);
    Task<object> GetJWKSAsync();
}

/// <summary>
/// Enterprise JWT token service implementation using DDD Domain Service.
/// Follows Single Responsibility and Dependency Inversion principles.
/// </summary>
public class TokenService : ITokenService
{
    private readonly ILogger<TokenService> _logger;
    private readonly Authentication.Domain.Services.IAuthenticationDomainService _authDomainService;
    private readonly Authentication.Interfaces.ISigningKeyService _signingKeyService;
    private readonly ICryptographicUtilityService _cryptographicUtilityService;
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;

    public TokenService(
        ILogger<TokenService> logger,
        Authentication.Domain.Services.IAuthenticationDomainService authDomainService,
        Authentication.Interfaces.ISigningKeyService signingKeyService,
        ICryptographicUtilityService cryptographicUtilityService,
        IOptionsMonitor<AuthenticationConfiguration> authConfig)
    {
        _logger = logger;
        _authDomainService = authDomainService;
        _signingKeyService = signingKeyService;
        _cryptographicUtilityService = cryptographicUtilityService;
        _authConfig = authConfig;
    }


    /// <summary>
    /// Creates RFC 9068 compliant JWT access token using Domain Service.
    /// Delegates to domain service following SRP and DIP principles.
    /// </summary>
    public async Task<string> CreateAccessTokenAsync(ClaimsPrincipal user, string clientId, string[] scopes, string? tenantId = null)
    {
        // Create user if needed
        var userId = Authentication.Domain.ValueObjects.UserId.Create(user.Identity?.Name ?? "anonymous");
        await _authDomainService.CreateUserAsync(userId.Value, tenantId ?? "default");

        // Issue access token through domain service
        var tokenEntity = await _authDomainService.IssueTokenAsync(
            userId, 
            clientId, 
            scopes, 
            Authentication.Domain.ValueObjects.TokenType.AccessToken, 
            TimeSpan.FromMinutes(15));

        return await GenerateJwtFromTokenEntity(tokenEntity, user, scopes);
    }

    /// <summary>
    /// Creates secure refresh token using Domain Service.
    /// Delegates to domain service following SRP and DIP principles.
    /// </summary>
    public async Task<string> CreateRefreshTokenAsync(ClaimsPrincipal user, string clientId, string? tenantId = null)
    {
        // Create user if needed
        var userId = Authentication.Domain.ValueObjects.UserId.Create(user.Identity?.Name ?? "anonymous");
        await _authDomainService.CreateUserAsync(userId.Value, tenantId ?? "default");

        // Issue refresh token through domain service
        var tokenEntity = await _authDomainService.IssueTokenAsync(
            userId, 
            clientId, 
            new[] { "refresh_token" }, 
            Authentication.Domain.ValueObjects.TokenType.RefreshToken, 
            TimeSpan.FromDays(30));

        return await GenerateJwtFromTokenEntity(tokenEntity, user, tokenEntity.Scopes);
    }

    /// <summary>
    /// Validates JWT token using Domain Service.
    /// Delegates to domain service following SRP and DIP principles.
    /// </summary>
    public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        // Extract token ID from JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var tokenId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        if (string.IsNullOrEmpty(tokenId))
        {
            return null;
        }

        // Use domain service for validation
        bool isValid = await _authDomainService.ValidateTokenAsync(tokenId);
        if (!isValid)
        {
            return null;
        }

        // Return principal from JWT if token is valid
        try
        {
            var principal = tokenHandler.ValidateToken(token, GetValidationParameters(), out var validatedToken);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Revokes token using Domain Service.
    /// Delegates to domain service following SRP and DIP principles.
    /// </summary>
    public async Task<bool> RevokeTokenAsync(string tokenId)
    {
        return await _authDomainService.RevokeTokenAsync(tokenId);
    }

    /// <summary>
    /// Checks if token is revoked using Domain Service.
    /// Delegates to domain service following SRP and DIP principles.
    /// </summary>
    public async Task<bool> IsTokenRevokedAsync(string tokenId)
    {
        var isValid = await _authDomainService.ValidateTokenAsync(tokenId);
        return !isValid;
    }

    /// <summary>
    /// Generates JWT string from TokenEntity following DDD patterns.
    /// Single responsibility for JWT generation logic.
    /// </summary>
    private async Task<string> GenerateJwtFromTokenEntity(Authentication.Domain.ValueObjects.AuthenticationToken tokenEntity, ClaimsPrincipal user, string[] scopes)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Identity?.Name ?? "unknown"),
            new(JwtRegisteredClaimNames.Jti, tokenEntity.TokenId),
            new("client_id", tokenEntity.ClientId),
            new("tenant", tokenEntity.TenantId),
            new("scope", string.Join(" ", scopes))
        };

        // Add tool-specific permissions
        foreach (var scope in scopes)
        {
            if (scope.StartsWith("mcp:"))
            {
                claims.Add(new Claim("tools", scope.Substring(4)));
            }
        }

        var config = _authConfig.CurrentValue;
        var issuer = config.OAuth.Issuer;
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = tokenEntity.ExpiresAt,
            Issuer = issuer,
            Audience = issuer, // Set audience to same value as issuer for validation
            SigningCredentials = _signingKeyService.GetSigningCredentials()
        };

        // Set RFC 9068 compliant 'at+jwt' type for access tokens
        if (tokenEntity.Type == Authentication.Domain.ValueObjects.TokenType.AccessToken)
        {
            tokenDescriptor.AdditionalHeaderClaims = new Dictionary<string, object> { { "typ", "at+jwt" } };
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        // Set RFC 9068 compliant 'at+jwt' type for access tokens using JwtSecurityToken directly
        if (tokenEntity.Type == Authentication.Domain.ValueObjects.TokenType.AccessToken && token is JwtSecurityToken jwtToken)
        {
            jwtToken.Header["typ"] = "at+jwt";
        }
        
        _logger.LogDebug("Generated JWT for token {TokenId} of type {Type}",
            tokenEntity.TokenId, tokenEntity.Type);
        
        return tokenHandler.WriteToken(token);
    }

    private TokenValidationParameters GetValidationParameters()
    {
        var config = _authConfig.CurrentValue;
        var issuer = config.OAuth.Issuer;
        
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = issuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKeyService.GetSigningKey(),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Returns JWKS (JSON Web Key Set) for enterprise token validation.
    /// </summary>
    public async Task<object> GetJWKSAsync()
    {
        try
        {
            _logger.LogDebug("Generating JWKS from signing key");
            
            var signingKey = _signingKeyService.GetSigningKey();
            if (signingKey is not RsaSecurityKey rsaKey)
            {
                _logger.LogError("Signing key is not RSA type: {KeyType}", signingKey.GetType());
                return new { keys = new object[] { } };
            }

            RSA rsa;
            if (rsaKey.Rsa != null)
            {
                rsa = rsaKey.Rsa;
            }
            else if (rsaKey.Parameters.Modulus != null)
            {
                rsa = RSA.Create();
                rsa.ImportParameters(rsaKey.Parameters);
            }
            else
            {
                _logger.LogError("RSA key has no Rsa instance or Parameters");
                return new { keys = new object[] { } };
            }

            var parameters = rsa.ExportParameters(false);
            
            if (parameters.Modulus == null || parameters.Exponent == null)
            {
                _logger.LogError("RSA parameters are null");
                return new { keys = new object[] { } };
            }

            // Convert RSA parameters to Base64URL encoding using centralized service
            var n = _cryptographicUtilityService.ToBase64Url(parameters.Modulus);
            var e = _cryptographicUtilityService.ToBase64Url(parameters.Exponent);

            var jwk = new
            {
                kty = "RSA",
                use = "sig",
                kid = rsaKey.KeyId ?? "enterprise-signing-key",
                alg = "RS256",
                n = n,
                e = e
            };

            _logger.LogInformation("Generated JWKS with key ID {KeyId}, modulus length {ModulusLength}", 
                jwk.kid, parameters.Modulus.Length);

            return new { keys = new[] { jwk } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate JWKS: {Message}", ex.Message);
            return new { keys = new object[] { } };
        }
    }

}