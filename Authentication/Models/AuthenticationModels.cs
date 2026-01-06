using System.Security.Claims;

namespace Authentication.Models;

/// <summary>
/// Supported authentication modes for enterprise deployment.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// No authentication required - development environments only.
    /// </summary>
    Disabled,

    /// <summary>
    /// Delegate authentication to external enterprise identity provider.
    /// </summary>
    ResourceServer,

    /// <summary>
    /// Full OAuth 2.1 authorization server - complete enterprise control.
    /// </summary>
    AuthorizationServer,

    /// <summary>
    /// Hybrid mode supporting both resource and authorization server patterns.
    /// </summary>
    Hybrid,

    /// <summary>
    /// Zero trust mode with continuous validation and threat detection.
    /// </summary>
    ZeroTrust
}

/// <summary>
/// Authentication features that may be conditionally supported.
/// </summary>
public enum AuthenticationFeature
{
    DynamicClientRegistration,
    WebAuthnSupport,
    TokenRevocation,
    MultiTenantIsolation,
    RealTimeAuditing,
    ThreatDetection,
    ExternalIdPIntegration
}

/// <summary>
/// Represents an authentication request with enterprise context.
/// </summary>
public class AuthenticationRequest
{
    /// <summary>
    /// The Bearer token from Authorization header, if present.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// The requesting client's IP address for security analysis.
    /// </summary>
    public string ClientIPAddress { get; set; } = string.Empty;

    /// <summary>
    /// User agent string for device fingerprinting.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Client certificate for mutual TLS authentication, if present.
    /// </summary>
    public string? ClientCertificate { get; set; }

    /// <summary>
    /// The MCP tool being requested for contextual authorization.
    /// </summary>
    public string RequestedTool { get; set; } = string.Empty;

    /// <summary>
    /// Tenant context for multi-tenant deployments.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Timestamp of the authentication request.
    /// </summary>
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional context headers for enterprise security analysis.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
}

/// <summary>
/// Result of authentication validation with enterprise context.
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Indicates if authentication was successful.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// User claims principal if authentication successful.
    /// </summary>
    public ClaimsPrincipal? User { get; set; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for programmatic error handling.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Additional authentication context for enterprise auditing.
    /// </summary>
    public AuthenticationContext? Context { get; set; }

    /// <summary>
    /// WWW-Authenticate challenge information for client.
    /// </summary>
    public string? Challenge { get; set; }

    /// <summary>
    /// Creates successful authentication result.
    /// </summary>
    public static AuthenticationResult Success(ClaimsPrincipal user, AuthenticationContext? context = null)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = true,
            User = user,
            Context = context
        };
    }

    /// <summary>
    /// Creates failed authentication result.
    /// </summary>
    public static AuthenticationResult Failure(string errorMessage, string? errorCode = null, string? challenge = null)
    {
        return new AuthenticationResult
        {
            IsAuthenticated = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            Challenge = challenge
        };
    }
}

/// <summary>
/// Enterprise authentication context for security analysis and auditing.
/// </summary>
public class AuthenticationContext
{
    /// <summary>
    /// Unique identifier for this authentication session.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Tenant identifier for multi-tenant isolation.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Authentication method used (OAuth, WebAuthn, etc.).
    /// </summary>
    public string AuthenticationMethod { get; set; } = string.Empty;

    /// <summary>
    /// Device information for enterprise device compliance.
    /// </summary>
    public DeviceInfo? Device { get; set; }

    /// <summary>
    /// Geographic location for location-based access control.
    /// </summary>
    public LocationInfo? Location { get; set; }

    /// <summary>
    /// Risk assessment score for this authentication.
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Unknown;

    /// <summary>
    /// Compliance frameworks that apply to this authentication.
    /// </summary>
    public string[] ApplicableCompliance { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Enterprise security policy for authentication decisions.
/// </summary>
public class SecurityPolicy
{
    /// <summary>
    /// Policy identifier for tracking and auditing.
    /// </summary>
    public string PolicyId { get; set; } = string.Empty;

    /// <summary>
    /// Policy name for human identification.
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Tenant this policy applies to, if tenant-specific.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Tool-specific permission requirements.
    /// </summary>
    public Dictionary<string, ToolAccessLevel> ToolPermissions { get; set; } = new();

    /// <summary>
    /// Authentication requirements for different risk levels.
    /// </summary>
    public Dictionary<RiskLevel, string> RiskRequirements { get; set; } = new();

    /// <summary>
    /// Maximum session duration before re-authentication required.
    /// </summary>
    public TimeSpan MaxSessionDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Geographic restrictions for access.
    /// </summary>
    public GeographicPolicy? GeographicRestrictions { get; set; }
}

/// <summary>
/// Result of enterprise policy evaluation.
/// </summary>
public class PolicyResult
{
    /// <summary>
    /// Indicates if policy evaluation passed.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Reason for policy decision.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Required actions if access is conditional.
    /// </summary>
    public string[] RequiredActions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Additional authentication required, if any.
    /// </summary>
    public AuthenticationChallenge? AdditionalAuthRequired { get; set; }
}

/// <summary>
/// Tool access levels for enterprise authorization.
/// </summary>
public enum ToolAccessLevel
{
    /// <summary>
    /// No access to the tool.
    /// </summary>
    Denied,

    /// <summary>
    /// Read-only operations allowed.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Limited write operations allowed.
    /// </summary>
    LimitedWrite,

    /// <summary>
    /// Full access to all tool operations.
    /// </summary>
    FullAccess,

    /// <summary>
    /// Access requires supervisor approval.
    /// </summary>
    SupervisorApproval
}

/// <summary>
/// Risk levels for enterprise authentication decisions.
/// </summary>
public enum RiskLevel
{
    Unknown,
    VeryLow,
    Low, 
    Medium,
    High,
    Critical
}

/// <summary>
/// Device information for enterprise device compliance.
/// </summary>
public class DeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public bool IsManaged { get; set; }
    public bool IsCompliant { get; set; }
    public DateTime LastComplianceCheck { get; set; }
}

/// <summary>
/// Location information for geographic access control.
/// </summary>
public class LocationInfo
{
    public string Country { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsKnownLocation { get; set; }
    public bool IsOfficeLocation { get; set; }
}

/// <summary>
/// Geographic policy for location-based access control.
/// </summary>
public class GeographicPolicy
{
    public string[] AllowedCountries { get; set; } = Array.Empty<string>();
    public string[] BlockedCountries { get; set; } = Array.Empty<string>();
    public bool RequireKnownLocation { get; set; }
    public bool AllowOfficeOnly { get; set; }
}

/// <summary>
/// Authentication challenge for step-up authentication.
/// </summary>
public class AuthenticationChallenge
{
    public string ChallengeType { get; set; } = string.Empty;
    public string ChallengeData { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string[] AllowedMethods { get; set; } = Array.Empty<string>();
}