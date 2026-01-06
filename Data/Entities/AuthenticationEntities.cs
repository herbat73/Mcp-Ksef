using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities;

/// <summary>
/// OAuth client registration entity for enterprise client management.
/// </summary>
[Table("oauth_clients")]
public class OAuthClient
{
    /// <summary>
    /// Unique client identifier.
    /// </summary>
    [Key]
    [StringLength(255)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client display name for enterprise identification.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Client secret hash for confidential clients.
    /// </summary>
    [StringLength(500)]
    public string? ClientSecretHash { get; set; }

    /// <summary>
    /// Registered redirect URIs for OAuth security.
    /// </summary>
    [Required]
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>
    /// Client type (public or confidential).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ClientType { get; set; } = "public";

    /// <summary>
    /// Allowed OAuth grant types for this client.
    /// </summary>
    public string GrantTypes { get; set; } = "authorization_code";

    /// <summary>
    /// Allowed scopes for this client.
    /// </summary>
    public string Scopes { get; set; } = "mcp:tools";

    /// <summary>
    /// Client approval status for enterprise security.
    /// </summary>
    public ClientApprovalStatus ApprovalStatus { get; set; } = ClientApprovalStatus.Pending;

    /// <summary>
    /// Enterprise admin who approved this client.
    /// </summary>
    [StringLength(255)]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Tenant this client belongs to for multi-tenant isolation.
    /// </summary>
    [StringLength(255)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Client registration timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Client approval timestamp.
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Client certificate thumbprint for mutual TLS.
    /// </summary>
    [StringLength(255)]
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Client expiration for enterprise lifecycle management.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Client activity status.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// OAuth authorization code entity for PKCE flow security.
/// </summary>
[Table("oauth_authorization_codes")]
public class AuthorizationCode
{
    /// <summary>
    /// Unique authorization code identifier.
    /// </summary>
    [Key]
    [StringLength(255)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Client that requested this authorization code.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// User who authorized this code.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// PKCE code challenge for authorization code protection.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string CodeChallenge { get; set; } = string.Empty;

    /// <summary>
    /// PKCE code challenge method (S256).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string CodeChallengeMethod { get; set; } = "S256";

    /// <summary>
    /// Redirect URI for this authorization.
    /// </summary>
    [Required]
    [StringLength(2000)]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Requested scopes for this authorization.
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// Tenant context for multi-tenant isolation.
    /// </summary>
    [StringLength(255)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Authorization code expiration timestamp.
    /// </summary>
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);

    /// <summary>
    /// Code creation timestamp for audit trails.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this code has been used (single-use security).
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Navigation property to OAuth client.
    /// </summary>
    public virtual OAuthClient Client { get; set; } = null!;
}

/// <summary>
/// WebAuthn credential entity for enterprise passwordless authentication.
/// </summary>
[Table("webauthn_credentials")]
public class WebAuthnCredential
{
    /// <summary>
    /// Unique credential identifier from WebAuthn.
    /// </summary>
    [Key]
    [StringLength(255)]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>
    /// User this credential belongs to.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User handle for WebAuthn credential binding.
    /// </summary>
    [Required]
    public byte[] UserHandle { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Public key for signature verification.
    /// </summary>
    [Required]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Signature counter for replay protection.
    /// </summary>
    public uint SignatureCounter { get; set; } = 0;

    /// <summary>
    /// Authenticator attestation data for enterprise validation.
    /// </summary>
    public byte[]? AttestationData { get; set; }

    /// <summary>
    /// Authenticator AAGUID for device identification.
    /// </summary>
    [StringLength(255)]
    public string? AuthenticatorAAGUID { get; set; }

    /// <summary>
    /// Device type for enterprise policy enforcement.
    /// </summary>
    [StringLength(100)]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise device management status.
    /// </summary>
    public bool IsEnterpriseManaged { get; set; } = false;

    /// <summary>
    /// Tenant this credential belongs to.
    /// </summary>
    [StringLength(255)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Credential creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful authentication timestamp.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Credential expiration for enterprise lifecycle management.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Credential activity status.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// User entity for enterprise user management.
/// </summary>
[Table("users")]
public class User
{
    /// <summary>
    /// Unique user identifier.
    /// </summary>
    [Key]
    [StringLength(255)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User principal name (email) for enterprise identity.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for user-friendly identification.
    /// </summary>
    [StringLength(500)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise roles for authorization decisions.
    /// </summary>
    public string Roles { get; set; } = string.Empty;

    /// <summary>
    /// Tool permissions for AI access control.
    /// </summary>
    public string ToolPermissions { get; set; } = string.Empty;

    /// <summary>
    /// Tenant this user belongs to.
    /// </summary>
    [StringLength(255)]
    public string? TenantId { get; set; }

    /// <summary>
    /// User creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last authentication timestamp.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// User account status.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property to WebAuthn credentials.
    /// </summary>
    public virtual ICollection<WebAuthnCredential> WebAuthnCredentials { get; set; } = new List<WebAuthnCredential>();
}

/// <summary>
/// Token revocation entity for enterprise token lifecycle management.
/// </summary>
[Table("token_revocations")]
public class TokenRevocation
{
    /// <summary>
    /// Unique revocation identifier.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Token identifier (JTI claim) for revocation tracking.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string TokenId { get; set; } = string.Empty;

    /// <summary>
    /// User whose token was revoked.
    /// </summary>
    [Required]
    [StringLength(255)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Reason for token revocation.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string RevocationReason { get; set; } = string.Empty;

    /// <summary>
    /// Enterprise admin who initiated revocation.
    /// </summary>
    [StringLength(255)]
    public string? RevokedBy { get; set; }

    /// <summary>
    /// Tenant context for multi-tenant token management.
    /// </summary>
    [StringLength(255)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Token revocation timestamp.
    /// </summary>
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Token expiration timestamp for cleanup optimization.
    /// </summary>
    public DateTime TokenExpiresAt { get; set; }
}

/// <summary>
/// Audit log entity for enterprise compliance and security monitoring.
/// </summary>
[Table("audit_logs")]
public class AuditLogEntry
{
    /// <summary>
    /// Unique audit entry identifier.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Event type for categorization and analysis.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// User involved in the event.
    /// </summary>
    [StringLength(255)]
    public string? UserId { get; set; }

    /// <summary>
    /// Tenant context for multi-tenant auditing.
    /// </summary>
    [StringLength(255)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Client involved in the event.
    /// </summary>
    [StringLength(255)]
    public string? ClientId { get; set; }

    /// <summary>
    /// IP address for security analysis.
    /// </summary>
    [StringLength(100)]
    public string? IPAddress { get; set; }

    /// <summary>
    /// Tool accessed during this event.
    /// </summary>
    [StringLength(255)]
    public string? ToolName { get; set; }

    /// <summary>
    /// Event outcome (success, failure, error).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// Detailed event description.
    /// </summary>
    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// Additional event data as JSON for flexibility.
    /// </summary>
    public string? AdditionalData { get; set; }

    /// <summary>
    /// Event timestamp for chronological analysis.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Risk level associated with this event.
    /// </summary>
    [StringLength(50)]
    public string RiskLevel { get; set; } = "Unknown";

    /// <summary>
    /// Compliance frameworks this event satisfies.
    /// </summary>
    [StringLength(500)]
    public string? ComplianceFrameworks { get; set; }
}

/// <summary>
/// Client approval status for enterprise governance.
/// </summary>
public enum ClientApprovalStatus
{
    /// <summary>
    /// Client registration pending enterprise approval.
    /// </summary>
    Pending,

    /// <summary>
    /// Client approved for use by enterprise admin.
    /// </summary>
    Approved,

    /// <summary>
    /// Client rejected by enterprise security policy.
    /// </summary>
    Rejected,

    /// <summary>
    /// Client suspended due to security concerns.
    /// </summary>
    Suspended,

    /// <summary>
    /// Client revoked and no longer valid.
    /// </summary>
    Revoked
}