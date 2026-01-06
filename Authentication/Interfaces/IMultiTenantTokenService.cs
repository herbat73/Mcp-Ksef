using System.Security.Claims;
using Authentication.Models;

namespace Authentication.Interfaces;

/// <summary>
/// Multi-tenant token service for enterprise tenant isolation.
/// Provides tenant-specific token management with complete isolation.
/// </summary>
public interface IMultiTenantTokenService
{
    /// <summary>
    /// Issues enterprise token with tenant-specific isolation and policies.
    /// </summary>
    /// <param name="tenant">Tenant context for isolation</param>
    /// <param name="user">User context for authentication</param>
    /// <returns>Enterprise token with tenant-specific claims and encryption</returns>
    Task<EnterpriseToken> IssueTokenAsync(TenantContext tenant, UserContext user);

    /// <summary>
    /// Validates tenant access for user with enterprise policies.
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="user">User principal to validate</param>
    /// <returns>True if user has valid access to tenant</returns>
    Task<bool> ValidateTenantAccessAsync(string tenantId, ClaimsPrincipal user);

    /// <summary>
    /// Gets tool permissions for user within specific tenant context.
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Tool permissions specific to tenant and user</returns>
    Task<ToolPermissions> GetToolPermissionsAsync(string tenantId, string userId);

    /// <summary>
    /// Revokes all tokens for tenant (enterprise admin control).
    /// </summary>
    /// <param name="tenantId">Tenant to revoke tokens for</param>
    /// <param name="reason">Revocation reason for audit trail</param>
    /// <returns>Number of tokens revoked</returns>
    Task<int> RevokeAllTenantTokensAsync(string tenantId, string reason);

    /// <summary>
    /// Gets tenant-specific token statistics for enterprise monitoring.
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <returns>Token usage statistics for tenant</returns>
    Task<TenantTokenStatistics> GetTenantTokenStatisticsAsync(string tenantId);

    /// <summary>
    /// Validates token audience binding to prevent cross-tenant token usage.
    /// </summary>
    /// <param name="token">Token to validate</param>
    /// <param name="tenantId">Expected tenant ID</param>
    /// <returns>True if token is valid for the specified tenant</returns>
    Task<bool> ValidateTokenTenantBindingAsync(string token, string tenantId);
}

/// <summary>
/// Enterprise token with tenant-specific isolation and metadata.
/// </summary>
public class EnterpriseToken
{
    /// <summary>
    /// JWT access token string.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for extended sessions.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Token expiration timestamp.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Tenant this token is bound to.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// User this token was issued for.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Client this token was issued to.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Scopes granted for this token.
    /// </summary>
    public string[] Scopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Tool permissions for this token within tenant context.
    /// </summary>
    public Dictionary<string, ToolAccessLevel> ToolPermissions { get; set; } = new();

    /// <summary>
    /// Enterprise audit information for compliance.
    /// </summary>
    public TokenAuditInfo AuditInfo { get; set; } = new();
}

/// <summary>
/// Tenant context for enterprise isolation and policy enforcement.
/// </summary>
public class TenantContext
{
    /// <summary>
    /// Unique tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant domain for enterprise integration.
    /// </summary>
    public string TenantDomain { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise authentication policy for this tenant.
    /// </summary>
    public AuthenticationPolicy Policy { get; set; } = new();

    /// <summary>
    /// Tenant-specific encryption keys for isolation.
    /// </summary>
    public EncryptionKeySet Keys { get; set; } = new();

    /// <summary>
    /// Audit configuration for tenant compliance requirements.
    /// </summary>
    public AuditConfiguration AuditConfig { get; set; } = new();

    /// <summary>
    /// Tenant display name for enterprise identification.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Tenant status for enterprise lifecycle management.
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Compliance frameworks required for this tenant.
    /// </summary>
    public string[] ComplianceFrameworks { get; set; } = Array.Empty<string>();
}

/// <summary>
/// User context for enterprise authentication and authorization.
/// </summary>
public class UserContext
{
    /// <summary>
    /// User identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User principal name (email).
    /// </summary>
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>
    /// User display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise roles for authorization.
    /// </summary>
    public string[] Roles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Tenant memberships for multi-tenant access.
    /// </summary>
    public TenantMembership[] TenantMemberships { get; set; } = Array.Empty<TenantMembership>();

    /// <summary>
    /// Authentication method used.
    /// </summary>
    public string AuthenticationMethod { get; set; } = string.Empty;

    /// <summary>
    /// Device information for enterprise device compliance.
    /// </summary>
    public DeviceInfo? Device { get; set; }
}

/// <summary>
/// Tool permissions within enterprise tenant context.
/// </summary>
public class ToolPermissions
{
    /// <summary>
    /// Tenant these permissions apply to.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// User these permissions apply to.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tool-specific access levels.
    /// </summary>
    public Dictionary<string, ToolAccessLevel> Tools { get; set; } = new();

    /// <summary>
    /// Category-level permissions.
    /// </summary>
    public Dictionary<string, ToolAccessLevel> Categories { get; set; } = new();

    /// <summary>
    /// Session-specific permissions that may be elevated.
    /// </summary>
    public Dictionary<string, ToolAccessLevel> SessionPermissions { get; set; } = new();

    /// <summary>
    /// Permission expiration for time-limited access.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Enterprise authentication policy for tenant-specific security.
/// </summary>
public class AuthenticationPolicy
{
    /// <summary>
    /// Required authentication methods for this tenant.
    /// </summary>
    public string[] RequiredAuthMethods { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Maximum session duration for this tenant.
    /// </summary>
    public TimeSpan MaxSessionDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Require re-authentication for sensitive tools.
    /// </summary>
    public bool RequireReauthForSensitiveTools { get; set; } = true;

    /// <summary>
    /// Tool-specific policies for this tenant.
    /// </summary>
    public Dictionary<string, ToolPolicy> ToolPolicies { get; set; } = new();

    /// <summary>
    /// Geographic restrictions for this tenant.
    /// </summary>
    public GeographicPolicy? GeographicRestrictions { get; set; }

    /// <summary>
    /// Device compliance requirements.
    /// </summary>
    public DevicePolicy DeviceRequirements { get; set; } = new();
}

/// <summary>
/// Additional supporting types for enterprise multi-tenant architecture.
/// </summary>

public class EncryptionKeySet
{
    public string SigningKey { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
    public DateTime KeyRotationDate { get; set; }
}

public class AuditConfiguration
{
    public bool EnableComprehensiveLogging { get; set; } = true;
    public string[] ComplianceFrameworks { get; set; } = Array.Empty<string>();
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(2555); // 7 years
}

public class TenantTokenStatistics
{
    public string TenantId { get; set; } = string.Empty;
    public int ActiveTokens { get; set; }
    public int TotalTokensIssued { get; set; }
    public int RevokedTokens { get; set; }
    public DateTime LastTokenIssuedAt { get; set; }
}

public class TokenAuditInfo
{
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string IssuedBy { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

public class TenantMembership
{
    public string TenantId { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ToolPolicy
{
    public ToolAccessLevel DefaultAccess { get; set; } = ToolAccessLevel.Denied;
    public bool RequireApproval { get; set; } = false;
    public TimeSpan? SessionTimeout { get; set; }
}

public class DevicePolicy
{
    public bool RequireManagedDevices { get; set; } = false;
    public bool RequireCompliantDevices { get; set; } = true;
    public string[] AllowedDeviceTypes { get; set; } = Array.Empty<string>();
}

public enum TenantStatus
{
    Active,
    Suspended,
    Disabled,
    PendingActivation
}