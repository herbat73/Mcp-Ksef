using Authentication.Models;
using Authentication.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Authentication.Services;

/// <summary>
/// Enterprise OAuth policy service for tenant-specific security controls.
/// Implements policy-driven OAuth configuration per enterprise plan.
/// </summary>
public interface IEnterpriseOAuthPolicyService
{
    /// <summary>
    /// Gets OAuth policy for specific tenant.
    /// </summary>
    Task<EnterpriseOAuthPolicy> GetTenantPolicyAsync(string tenantId);

    /// <summary>
    /// Validates client registration against enterprise policy.
    /// </summary>
    Task<ClientRegistrationPolicyResult> ValidateClientRegistrationAsync(
        ClientRegistrationRequest request, string tenantId);

    /// <summary>
    /// Evaluates token issuance policy for tenant and user.
    /// </summary>
    Task<TokenIssuancePolicyResult> EvaluateTokenIssuancePolicyAsync(
        string tenantId, string userId, string clientId, string[] requestedScopes);

    /// <summary>
    /// Gets scope management policy for tenant.
    /// </summary>
    Task<ScopeManagementPolicy> GetScopePolicyAsync(string tenantId);
}

/// <summary>
/// Enterprise OAuth policy service implementation.
/// </summary>
public class EnterpriseOAuthPolicyService : IEnterpriseOAuthPolicyService
{
    private readonly ILogger<EnterpriseOAuthPolicyService> _logger;
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    
    // Tenant-specific OAuth policies
    private readonly Dictionary<string, EnterpriseOAuthPolicy> _tenantPolicies = new();

    public EnterpriseOAuthPolicyService(
        ILogger<EnterpriseOAuthPolicyService> logger,
        IOptionsMonitor<AuthenticationConfiguration> authConfig)
    {
        _logger = logger;
        _authConfig = authConfig;
        
        InitializeTenantPolicies();
    }

    /// <summary>
    /// Gets enterprise OAuth policy for specific tenant.
    /// </summary>
    public async Task<EnterpriseOAuthPolicy> GetTenantPolicyAsync(string tenantId)
    {
        if (_tenantPolicies.TryGetValue(tenantId, out var policy))
        {
            _logger.LogDebug("Retrieved OAuth policy for tenant {TenantId}", tenantId);
            return policy;
        }

        // Return default policy for unknown tenants
        _logger.LogWarning("No specific OAuth policy found for tenant {TenantId}, using default", tenantId);
        return _tenantPolicies["default"];
    }

    /// <summary>
    /// Validates client registration against enterprise policies.
    /// </summary>
    public async Task<ClientRegistrationPolicyResult> ValidateClientRegistrationAsync(
        ClientRegistrationRequest request, string tenantId)
    {
        try
        {
            var policy = await GetTenantPolicyAsync(tenantId);
            
            // Validate client registration policy
            if (!policy.ClientRegistration.EnableDynamicRegistration)
            {
                return ClientRegistrationPolicyResult.Denied("Dynamic client registration disabled for tenant");
            }

            if (policy.ClientRegistration.RequireClientCertificates && 
                string.IsNullOrEmpty(request.ClientCertificateThumbprint))
            {
                return ClientRegistrationPolicyResult.Denied("Client certificate required by tenant policy");
            }

            // Validate redirect URIs against policy
            foreach (var redirectUri in request.RedirectUris)
            {
                if (!IsRedirectUriAllowed(redirectUri, policy.ClientRegistration.AllowedRedirectHosts))
                {
                    return ClientRegistrationPolicyResult.Denied($"Redirect URI {redirectUri} not allowed by tenant policy");
                }
            }

            // Check if admin approval required
            if (policy.ClientRegistration.RequireAdminApproval)
            {
                return ClientRegistrationPolicyResult.PendingApproval("Client registration requires enterprise admin approval");
            }

            return ClientRegistrationPolicyResult.Approved("Client registration approved by policy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client registration for tenant {TenantId}", tenantId);
            return ClientRegistrationPolicyResult.Error("Policy validation error");
        }
    }

    /// <summary>
    /// Evaluates token issuance policy for enterprise controls.
    /// </summary>
    public async Task<TokenIssuancePolicyResult> EvaluateTokenIssuancePolicyAsync(
        string tenantId, string userId, string clientId, string[] requestedScopes)
    {
        try
        {
            var policy = await GetTenantPolicyAsync(tenantId);
            
            // Validate token issuance policy
            var allowedScopes = new List<string>();
            var deniedScopes = new List<string>();

            foreach (var scope in requestedScopes)
            {
                if (IsScopeAllowed(scope, policy.ScopeManagement, userId))
                {
                    allowedScopes.Add(scope);
                }
                else
                {
                    deniedScopes.Add(scope);
                    _logger.LogWarning("Scope {Scope} denied for user {User} in tenant {Tenant}",
                        scope, userId, tenantId);
                }
            }

            return new TokenIssuancePolicyResult
            {
                IsAllowed = allowedScopes.Count > 0,
                AllowedScopes = allowedScopes.ToArray(),
                DeniedScopes = deniedScopes.ToArray(),
                TokenLifetime = policy.TokenIssuance.AccessTokenLifetime,
                RequireReauth = policy.TokenIssuance.RequirePeriodicReauth,
                AuditLevel = policy.AuditPolicy.RequiredLevel
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating token issuance policy for tenant {TenantId}", tenantId);
            return TokenIssuancePolicyResult.Error("Policy evaluation error");
        }
    }

    /// <summary>
    /// Gets scope management policy for tenant.
    /// </summary>
    public async Task<ScopeManagementPolicy> GetScopePolicyAsync(string tenantId)
    {
        var policy = await GetTenantPolicyAsync(tenantId);
        return policy.ScopeManagement;
    }

    /// <summary>
    /// Validates if redirect URI is allowed by enterprise policy.
    /// </summary>
    private bool IsRedirectUriAllowed(string redirectUri, string[] allowedHosts)
    {
        if (allowedHosts.Length == 0)
            return true; // No restrictions

        var uri = new Uri(redirectUri);
        return allowedHosts.Contains(uri.Host) || uri.Host == "localhost";
    }

    /// <summary>
    /// Validates if scope is allowed for user in tenant.
    /// </summary>
    private bool IsScopeAllowed(string scope, ScopeManagementPolicy scopePolicy, string userId)
    {
        // Basic scope validation - will be enhanced with user role checking
        if (scopePolicy.AllowedScopes.Length > 0)
        {
            return scopePolicy.AllowedScopes.Contains(scope);
        }

        if (scopePolicy.DeniedScopes.Contains(scope))
        {
            return false;
        }

        // Default allow for mcp: scopes
        return scope.StartsWith("mcp:");
    }

    /// <summary>
    /// Initializes enterprise tenant policies for development.
    /// </summary>
    private void InitializeTenantPolicies()
    {
        // Default tenant policy
        _tenantPolicies["default"] = new EnterpriseOAuthPolicy
        {
            ClientRegistration = new ClientRegistrationPolicy
            {
                EnableDynamicRegistration = true,
                RequireClientCertificates = false,
                RequireAdminApproval = false,
                ClientCredentialExpiry = TimeSpan.FromDays(365),
                AllowedRedirectHosts = new[] { "localhost", "127.0.0.1" }
            },
            TokenIssuance = new TokenIssuancePolicy
            {
                AccessTokenLifetime = TimeSpan.FromMinutes(15),
                RefreshTokenLifetime = TimeSpan.FromDays(30),
                RequirePeriodicReauth = false,
                MaxConcurrentTokens = 10
            },
            ScopeManagement = new ScopeManagementPolicy
            {
                AllowedScopes = new[] { "mcp:tools", "mcp:math", "mcp:utility", "mcp:data", "mcp:reflection" },
                DeniedScopes = Array.Empty<string>(),
                RequireApprovalForScopes = Array.Empty<string>()
            },
            AuditPolicy = new AuditPolicy
            {
                RequiredLevel = AuditLevel.Standard,
                LogAllTokenOperations = true,
                RetentionPeriod = TimeSpan.FromDays(90)
            }
        };

        // Enterprise demo tenant with stricter policies
        _tenantPolicies["enterprise-demo"] = new EnterpriseOAuthPolicy
        {
            ClientRegistration = new ClientRegistrationPolicy
            {
                EnableDynamicRegistration = false, // Require manual registration
                RequireClientCertificates = true,
                RequireAdminApproval = true,
                ClientCredentialExpiry = TimeSpan.FromDays(90),
                AllowedRedirectHosts = new[] { "demo.company.com", "localhost" }
            },
            TokenIssuance = new TokenIssuancePolicy
            {
                AccessTokenLifetime = TimeSpan.FromMinutes(5), // Shorter for demo
                RefreshTokenLifetime = TimeSpan.FromDays(7),
                RequirePeriodicReauth = true,
                MaxConcurrentTokens = 3
            },
            ScopeManagement = new ScopeManagementPolicy
            {
                AllowedScopes = new[] { "mcp:tools", "mcp:math", "mcp:utility" }, // Restricted scopes
                DeniedScopes = new[] { "mcp:reflection" }, // No reflection tools
                RequireApprovalForScopes = new[] { "mcp:data" }
            },
            AuditPolicy = new AuditPolicy
            {
                RequiredLevel = AuditLevel.Comprehensive,
                LogAllTokenOperations = true,
                RetentionPeriod = TimeSpan.FromDays(2555) // 7 years for compliance
            }
        };

        _logger.LogInformation("Initialized OAuth policies for {TenantCount} tenants", _tenantPolicies.Count);
    }
}

/// <summary>
/// Enterprise OAuth policy for tenant-specific security configuration.
/// </summary>
public class EnterpriseOAuthPolicy
{
    public ClientRegistrationPolicy ClientRegistration { get; set; } = new();
    public TokenIssuancePolicy TokenIssuance { get; set; } = new();
    public ScopeManagementPolicy ScopeManagement { get; set; } = new();
    public AuditPolicy AuditPolicy { get; set; } = new();
}

/// <summary>
/// Client registration policy for enterprise governance.
/// </summary>
public class ClientRegistrationPolicy
{
    public bool EnableDynamicRegistration { get; set; } = false;
    public bool RequireClientCertificates { get; set; } = true;
    public bool RequireAdminApproval { get; set; } = true;
    public TimeSpan ClientCredentialExpiry { get; set; } = TimeSpan.FromDays(90);
    public string[] AllowedRedirectHosts { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Token issuance policy for enterprise security.
/// </summary>
public class TokenIssuancePolicy
{
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);
    public bool RequirePeriodicReauth { get; set; } = false;
    public int MaxConcurrentTokens { get; set; } = 10;
}

/// <summary>
/// Scope management policy for tool-level permissions.
/// </summary>
public class ScopeManagementPolicy
{
    public string[] AllowedScopes { get; set; } = Array.Empty<string>();
    public string[] DeniedScopes { get; set; } = Array.Empty<string>();
    public string[] RequireApprovalForScopes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Audit policy for enterprise compliance.
/// </summary>
public class AuditPolicy
{
    public AuditLevel RequiredLevel { get; set; } = AuditLevel.Standard;
    public bool LogAllTokenOperations { get; set; } = true;
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(90);
}

public enum AuditLevel
{
    None,
    Basic,
    Standard,
    Comprehensive,
    Forensic
}

/// <summary>
/// Supporting result types for policy evaluation.
/// </summary>

public class ClientRegistrationPolicyResult
{
    public bool IsAllowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ClientRegistrationStatus Status { get; set; }

    public static ClientRegistrationPolicyResult Approved(string reason) =>
        new() { IsAllowed = true, Reason = reason, Status = ClientRegistrationStatus.Approved };

    public static ClientRegistrationPolicyResult Denied(string reason) =>
        new() { IsAllowed = false, Reason = reason, Status = ClientRegistrationStatus.Denied };

    public static ClientRegistrationPolicyResult PendingApproval(string reason) =>
        new() { IsAllowed = false, Reason = reason, Status = ClientRegistrationStatus.PendingApproval };

    public static ClientRegistrationPolicyResult Error(string reason) =>
        new() { IsAllowed = false, Reason = reason, Status = ClientRegistrationStatus.Error };
}

public class TokenIssuancePolicyResult
{
    public bool IsAllowed { get; set; }
    public string[] AllowedScopes { get; set; } = Array.Empty<string>();
    public string[] DeniedScopes { get; set; } = Array.Empty<string>();
    public TimeSpan TokenLifetime { get; set; }
    public bool RequireReauth { get; set; }
    public AuditLevel AuditLevel { get; set; }

    public static TokenIssuancePolicyResult Error(string reason) =>
        new() { IsAllowed = false };
}

public class ClientRegistrationRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string[] RedirectUris { get; set; } = Array.Empty<string>();
    public string[] GrantTypes { get; set; } = { "authorization_code" };
    public string ClientType { get; set; } = "public";
    public string? ClientCertificateThumbprint { get; set; }
    public string TenantId { get; set; } = "default";
}

public enum ClientRegistrationStatus
{
    Approved,
    Denied,
    PendingApproval,
    Error
}