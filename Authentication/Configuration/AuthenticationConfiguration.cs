using Authentication.Models;

namespace Authentication.Configuration;

/// <summary>
/// Enterprise authentication configuration with adaptive security modes.
/// Follows .NET configuration patterns with enterprise security requirements.
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json binding.
    /// </summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// Current authentication mode for the enterprise deployment.
    /// </summary>
    public AuthenticationMode Mode { get; set; } = AuthenticationMode.Disabled;

    /// <summary>
    /// OAuth 2.1 configuration for authorization server mode.
    /// </summary>
    public OAuthConfiguration OAuth { get; set; } = new();

    /// <summary>
    /// WebAuthn configuration for enterprise passwordless authentication.
    /// </summary>
    public WebAuthnConfiguration WebAuthn { get; set; } = new();

    /// <summary>
    /// External identity provider configuration for resource server mode.
    /// </summary>
    public ExternalIdPConfiguration ExternalIdP { get; set; } = new();

    /// <summary>
    /// Multi-tenant configuration for enterprise isolation.
    /// </summary>
    public MultiTenantConfiguration MultiTenant { get; set; } = new();

    /// <summary>
    /// Enterprise security policies and compliance settings.
    /// </summary>
    public EnterpiseSecurityConfiguration Security { get; set; } = new();
}

/// <summary>
/// OAuth 2.1 configuration following enterprise security patterns.
/// </summary>
public class OAuthConfiguration
{
    /// <summary>
    /// OAuth issuer URL for token validation.
    /// Must be configured in appsettings.json - no default provided.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Access token lifetime for enterprise security balance.
    /// </summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Refresh token lifetime for extended enterprise sessions.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Enable dynamic client registration for ease of deployment.
    /// </summary>
    public bool EnableDynamicClientRegistration { get; set; } = false;

    /// <summary>
    /// Require enterprise approval for dynamically registered clients.
    /// </summary>
    public bool RequireClientApproval { get; set; } = true;

    /// <summary>
    /// Require client certificates for enhanced security.
    /// </summary>
    public bool RequireClientCertificates { get; set; } = false;

    /// <summary>
    /// Signing credentials configuration for enterprise key management.
    /// </summary>
    public SigningConfiguration Signing { get; set; } = new();
}

/// <summary>
/// WebAuthn configuration for enterprise passwordless authentication.
/// </summary>
public class WebAuthnConfiguration
{
    /// <summary>
    /// Enable WebAuthn passwordless authentication.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Server domain for WebAuthn credential binding.
    /// Must be configured in appsettings.json - no default provided.
    /// </summary>
    public string ServerDomain { get; set; } = string.Empty;

    /// <summary>
    /// Server display name for user-facing authentication.
    /// </summary>
    public string ServerName { get; set; } = "Enterprise MCP Server";

    /// <summary>
    /// Allowed origins for WebAuthn credential usage.
    /// Must be configured in appsettings.json - no default provided.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Require attestation validation for enterprise security.
    /// </summary>
    public bool RequireAttestationValidation { get; set; } = true;

    /// <summary>
    /// Allowed authenticator types per enterprise policy.
    /// </summary>
    public string[] AllowedAuthenticatorTypes { get; set; } = { "platform", "cross-platform" };

    /// <summary>
    /// Require user verification (biometric/PIN) for all authentications.
    /// </summary>
    public bool RequireUserVerification { get; set; } = true;

    /// <summary>
    /// Timeout for WebAuthn authentication challenges.
    /// </summary>
    public TimeSpan ChallengeTimeout { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// External identity provider configuration for enterprise integration.
/// </summary>
public class ExternalIdPConfiguration
{
    /// <summary>
    /// Identity provider type (AzureAD, AWSCognito, Generic).
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Azure Active Directory configuration.
    /// </summary>
    public AzureADConfiguration AzureAD { get; set; } = new();

    /// <summary>
    /// External authorization server URL for resource server mode.
    /// </summary>
    public string AuthorizationServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Client credentials for external IdP integration.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for confidential client authentication.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Required scopes for MCP tool access.
    /// </summary>
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Token validation parameters for external tokens.
    /// </summary>
    public TokenValidationConfiguration TokenValidation { get; set; } = new();
}

/// <summary>
/// Multi-tenant configuration for enterprise isolation.
/// </summary>
public class MultiTenantConfiguration
{
    /// <summary>
    /// Enable multi-tenant isolation features.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Tenant resolution strategy (subdomain, header, path, etc.).
    /// </summary>
    public TenantResolutionStrategy ResolutionStrategy { get; set; } = TenantResolutionStrategy.Header;

    /// <summary>
    /// Header name for tenant resolution when using header strategy.
    /// </summary>
    public string TenantHeaderName { get; set; } = "X-Tenant-ID";

    /// <summary>
    /// Default tenant ID when none specified.
    /// </summary>
    public string DefaultTenantId { get; set; } = "default";

    /// <summary>
    /// Enforce strict tenant isolation at all levels.
    /// </summary>
    public bool EnforceStrictIsolation { get; set; } = true;
}

/// <summary>
/// Enterprise security configuration for compliance and auditing.
/// </summary>
public class EnterpiseSecurityConfiguration
{
    /// <summary>
    /// Enable comprehensive audit logging for compliance.
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Enable real-time threat detection and response.
    /// </summary>
    public bool EnableThreatDetection { get; set; } = false;

    /// <summary>
    /// Compliance frameworks to enforce (SOC2, HIPAA, etc.).
    /// </summary>
    public string[] ComplianceFrameworks { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Rate limiting configuration for abuse prevention.
    /// </summary>
    public RateLimitConfiguration RateLimit { get; set; } = new();

    /// <summary>
    /// Geographic access control configuration.
    /// </summary>
    public GeographicSecurityConfiguration Geographic { get; set; } = new();
}

/// <summary>
/// Token validation configuration for external identity providers.
/// </summary>
public class TokenValidationConfiguration
{
    /// <summary>
    /// JWT issuer for token validation.
    /// </summary>
    public string ValidIssuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT audience for token validation.
    /// </summary>
    public string ValidAudience { get; set; } = string.Empty;

    /// <summary>
    /// JWT signing key or certificate for validation.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Clock skew tolerance for token timestamp validation.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Signing configuration for enterprise key management.
/// </summary>
public class SigningConfiguration
{
    /// <summary>
    /// Signing key for JWT token signing.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Signing algorithm (RS256, ES256, etc.).
    /// </summary>
    public string Algorithm { get; set; } = "RS256";

    /// <summary>
    /// Key rotation interval for enterprise security.
    /// </summary>
    public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Use Hardware Security Module for key operations.
    /// </summary>
    public bool UseHSM { get; set; } = false;
}

/// <summary>
/// Rate limiting configuration for enterprise abuse prevention.
/// </summary>
public class RateLimitConfiguration
{
    /// <summary>
    /// Enable rate limiting for authentication endpoints.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum authentication requests per minute per IP.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Maximum authentication requests per hour per user.
    /// </summary>
    public int RequestsPerHourPerUser { get; set; } = 1000;

    /// <summary>
    /// Burst allowance for legitimate high-frequency usage.
    /// </summary>
    public int BurstAllowance { get; set; } = 10;
}

/// <summary>
/// Geographic security configuration for location-based access control.
/// </summary>
public class GeographicSecurityConfiguration
{
    /// <summary>
    /// Enable geographic access control.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Allowed countries for access (ISO country codes).
    /// </summary>
    public string[] AllowedCountries { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Blocked countries for security (ISO country codes).
    /// </summary>
    public string[] BlockedCountries { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Require known/office locations for access.
    /// </summary>
    public bool RequireKnownLocations { get; set; } = false;
}

/// <summary>
/// Tenant resolution strategies for multi-tenant deployments.
/// </summary>
public enum TenantResolutionStrategy
{
    /// <summary>
    /// Resolve tenant from HTTP header.
    /// </summary>
    Header,

    /// <summary>
    /// Resolve tenant from subdomain.
    /// </summary>
    Subdomain,

    /// <summary>
    /// Resolve tenant from URL path.
    /// </summary>
    Path,

    /// <summary>
    /// Resolve tenant from JWT token claims.
    /// </summary>
    Token
}

/// <summary>
/// Azure Active Directory specific configuration.
/// </summary>
public class AzureADConfiguration
{
    /// <summary>
    /// Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD application client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD authority URL (base URL without /v2.0).
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Configured redirect URIs for OAuth flow.
    /// </summary>
    public string[] RedirectUris { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Required OAuth scopes.
    /// </summary>
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();
}