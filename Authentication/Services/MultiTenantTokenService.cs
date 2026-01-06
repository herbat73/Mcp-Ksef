using Authentication.Interfaces;
using Authentication.Models;
using Authentication.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Authentication.Services;

/// <summary>
/// Enterprise multi-tenant token service with complete tenant isolation.
/// Implements advanced security patterns for enterprise deployments.
/// </summary>
public class MultiTenantTokenService : IMultiTenantTokenService
{
    private readonly ILogger<MultiTenantTokenService> _logger;
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    private readonly ITokenService _baseTokenService;
    private readonly ICryptographicUtilityService _cryptographicUtilityService;
    
    // Tenant-specific encryption keys for isolation
    private readonly Dictionary<string, SecurityKey> _tenantSigningKeys = new();
    
    // In-memory tenant configurations for development
    private readonly Dictionary<string, TenantContext> _tenantConfigurations = new();

    public MultiTenantTokenService(
        ILogger<MultiTenantTokenService> logger,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ITokenService baseTokenService,
        ICryptographicUtilityService cryptographicUtilityService)
    {
        _logger = logger;
        _authConfig = authConfig;
        _baseTokenService = baseTokenService;
        _cryptographicUtilityService = cryptographicUtilityService;
        
        InitializeDefaultTenants();
    }

    /// <summary>
    /// Issues enterprise token with tenant-specific isolation.
    /// </summary>
    public async Task<EnterpriseToken> IssueTokenAsync(TenantContext tenant, UserContext user)
    {
        try
        {
            _logger.LogDebug("Issuing enterprise token for user {User} in tenant {Tenant}",
                user.UserId, tenant.TenantId);

            // Validate tenant access
            if (!await ValidateTenantUserAccessAsync(tenant, user))
            {
                throw new UnauthorizedAccessException($"User {user.UserId} not authorized for tenant {tenant.TenantId}");
            }

            // Create tenant-specific claims
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, user.UserPrincipalName),
                new(ClaimTypes.NameIdentifier, user.UserId),
                new("tenant", tenant.TenantId),
                new("tenant_domain", tenant.TenantDomain),
                new("auth_method", user.AuthenticationMethod)
            };

            // Add tenant-specific roles
            foreach (var membership in user.TenantMemberships.Where(m => m.TenantId == tenant.TenantId))
            {
                foreach (var role in membership.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // Create principal with tenant claims
            var identity = new ClaimsIdentity(claims, "enterprise");
            var principal = new ClaimsPrincipal(identity);

            // Get tenant-specific tool permissions
            var toolPermissions = await GetToolPermissionsAsync(tenant.TenantId, user.UserId);

            // Create access token with tenant-specific signing key
            var accessToken = await CreateTenantSpecificTokenAsync(principal, tenant, toolPermissions);

            // Create refresh token if policy allows
            string? refreshToken = null;
            if (tenant.Policy.MaxSessionDuration > TimeSpan.FromHours(1))
            {
                refreshToken = await CreateTenantRefreshTokenAsync(principal, tenant);
            }

            var enterpriseToken = new EnterpriseToken
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.Add(_authConfig.CurrentValue.OAuth.AccessTokenLifetime),
                TenantId = tenant.TenantId,
                UserId = user.UserId,
                ClientId = "enterprise-client", // Will be enhanced with actual client management
                Scopes = DetermineUserScopes(toolPermissions),
                ToolPermissions = toolPermissions.Tools,
                AuditInfo = new TokenAuditInfo
                {
                    IssuedAt = DateTime.UtcNow,
                    IssuedBy = "MultiTenantTokenService",
                    IPAddress = "system", // Will be enhanced with actual request context
                    UserAgent = "enterprise"
                }
            };

            _logger.LogInformation("Enterprise token issued for user {User} in tenant {Tenant} with {ToolCount} tool permissions",
                user.UserId, tenant.TenantId, toolPermissions.Tools.Count);

            return enterpriseToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue enterprise token for user {User} in tenant {Tenant}",
                user.UserId, tenant.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Validates tenant access with enterprise policies.
    /// </summary>
    public async Task<bool> ValidateTenantAccessAsync(string tenantId, ClaimsPrincipal user)
    {
        try
        {
            var tenantClaim = user.FindFirst("tenant")?.Value;
            if (tenantClaim != tenantId)
            {
                _logger.LogWarning("Tenant access validation failed: user token for {TokenTenant} used for {RequestedTenant}",
                    tenantClaim, tenantId);
                return false;
            }

            // Additional tenant validation logic
            if (!_tenantConfigurations.ContainsKey(tenantId))
            {
                _logger.LogWarning("Unknown tenant {TenantId} in access validation", tenantId);
                return false;
            }

            var tenant = _tenantConfigurations[tenantId];
            if (tenant.Status != TenantStatus.Active)
            {
                _logger.LogWarning("Inactive tenant {TenantId} access attempted", tenantId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tenant access for {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// Gets tool permissions for user within tenant context.
    /// </summary>
    public async Task<ToolPermissions> GetToolPermissionsAsync(string tenantId, string userId)
    {
        try
        {
            // Get tenant configuration
            if (!_tenantConfigurations.TryGetValue(tenantId, out var tenant))
            {
                throw new ArgumentException($"Unknown tenant {tenantId}");
            }

            // Create tenant-specific tool permissions
            var permissions = new ToolPermissions
            {
                TenantId = tenantId,
                UserId = userId,
                Tools = new Dictionary<string, ToolAccessLevel>(),
                Categories = new Dictionary<string, ToolAccessLevel>
                {
                    ["Math"] = ToolAccessLevel.FullAccess,
                    ["Utility"] = ToolAccessLevel.FullAccess,
                    ["Data"] = ToolAccessLevel.FullAccess,
                    ["Reflection"] = ToolAccessLevel.ReadOnly // Sensitive tools limited by default
                }
            };

            // Apply tenant-specific tool policies
            foreach (var toolPolicy in tenant.Policy.ToolPolicies)
            {
                permissions.Tools[toolPolicy.Key] = toolPolicy.Value.DefaultAccess;
            }

            _logger.LogDebug("Generated tool permissions for user {User} in tenant {Tenant}: {ToolCount} tools",
                userId, tenantId, permissions.Tools.Count);

            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tool permissions for user {User} in tenant {Tenant}",
                userId, tenantId);
            throw;
        }
    }

    /// <summary>
    /// Revokes all tokens for tenant (enterprise admin control).
    /// </summary>
    public async Task<int> RevokeAllTenantTokensAsync(string tenantId, string reason)
    {
        try
        {
            _logger.LogWarning("Revoking all tokens for tenant {TenantId}, reason: {Reason}",
                tenantId, reason);

            // This will be enhanced to work with actual token storage
            // For now, return 0 as no tokens are stored persistently
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking tenant tokens for {TenantId}", tenantId);
            throw;
        }
    }

    /// <summary>
    /// Gets tenant-specific token statistics.
    /// </summary>
    public async Task<TenantTokenStatistics> GetTenantTokenStatisticsAsync(string tenantId)
    {
        // Basic statistics for development - will be enhanced with real data
        return new TenantTokenStatistics
        {
            TenantId = tenantId,
            ActiveTokens = 0,
            TotalTokensIssued = 0,
            RevokedTokens = 0,
            LastTokenIssuedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Validates token tenant binding to prevent cross-tenant usage.
    /// </summary>
    public async Task<bool> ValidateTokenTenantBindingAsync(string token, string tenantId)
    {
        try
        {
            var principal = await _baseTokenService.ValidateTokenAsync(token);
            if (principal == null)
            {
                return false;
            }

            return await ValidateTenantAccessAsync(tenantId, principal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token tenant binding for {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// Creates tenant-specific JWT token with isolated signing key.
    /// </summary>
    private async Task<string> CreateTenantSpecificTokenAsync(
        ClaimsPrincipal principal, 
        TenantContext tenant, 
        ToolPermissions permissions)
    {
        // Get or create tenant-specific signing key
        var signingKey = GetTenantSigningKey(tenant.TenantId);
        
        var config = _authConfig.CurrentValue;
        var now = DateTime.UtcNow;
        var expiry = now.Add(config.OAuth.AccessTokenLifetime);

        var claims = principal.Claims.ToList();
        
        // Add tool permissions as claims
        foreach (var toolPermission in permissions.Tools)
        {
            claims.Add(new Claim($"tool:{toolPermission.Key}", toolPermission.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiry,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
            Issuer = $"{config.OAuth.Issuer}/tenant/{tenant.TenantId}",
            Audience = $"{config.OAuth.Issuer}/tenant/{tenant.TenantId}"
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Creates tenant-specific refresh token.
    /// </summary>
    private async Task<string> CreateTenantRefreshTokenAsync(ClaimsPrincipal principal, TenantContext tenant)
    {
        // Use base token service with tenant-specific claims
        var tenantPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(principal.Claims.Concat(new[]
            {
                new Claim("token_type", "refresh"),
                new Claim("tenant", tenant.TenantId)
            })));

        return await _baseTokenService.CreateRefreshTokenAsync(tenantPrincipal, "enterprise-client", tenant.TenantId);
    }

    /// <summary>
    /// Gets or creates tenant-specific signing key for isolation.
    /// </summary>
    private SecurityKey GetTenantSigningKey(string tenantId)
    {
        if (!_tenantSigningKeys.TryGetValue(tenantId, out var key))
        {
            // Use centralized cryptographic service to eliminate duplication
            var keyId = $"tenant-{tenantId}-signing-key";
            key = _cryptographicUtilityService.CreateRSASigningKey(2048, keyId);
            _tenantSigningKeys[tenantId] = key;
            
            _logger.LogInformation("Created tenant-specific signing key for {TenantId} using centralized service", tenantId);
        }
        
        return key;
    }

    /// <summary>
    /// Validates tenant user access with enterprise policies.
    /// </summary>
    private async Task<bool> ValidateTenantUserAccessAsync(TenantContext tenant, UserContext user)
    {
        // Check if user has membership in this tenant
        var membership = user.TenantMemberships.FirstOrDefault(m => m.TenantId == tenant.TenantId);
        if (membership == null || !membership.IsActive)
        {
            _logger.LogWarning("User {User} has no active membership in tenant {Tenant}",
                user.UserId, tenant.TenantId);
            return false;
        }

        // Additional tenant policy validation
        if (tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning("Tenant {Tenant} is not active (status: {Status})",
                tenant.TenantId, tenant.Status);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines user scopes based on tool permissions.
    /// </summary>
    private string[] DetermineUserScopes(ToolPermissions permissions)
    {
        var scopes = new List<string> { "mcp:tools" };
        
        foreach (var category in permissions.Categories)
        {
            if (category.Value != ToolAccessLevel.Denied)
            {
                scopes.Add($"mcp:{category.Key.ToLower()}");
            }
        }
        
        return scopes.ToArray();
    }

    /// <summary>
    /// Initializes default tenants for development.
    /// </summary>
    private void InitializeDefaultTenants()
    {
        // Default tenant for single-tenant deployments
        _tenantConfigurations["default"] = new TenantContext
        {
            TenantId = "default",
            TenantDomain = "localhost",
            DisplayName = "Default Tenant",
            Status = TenantStatus.Active,
            Policy = new AuthenticationPolicy
            {
                RequiredAuthMethods = new[] { "oauth" },
                MaxSessionDuration = TimeSpan.FromHours(8),
                RequireReauthForSensitiveTools = true,
                ToolPolicies = new Dictionary<string, ToolPolicy>
                {
                    ["ListAllTools"] = new() { DefaultAccess = ToolAccessLevel.ReadOnly },
                    ["GetServerMetadata"] = new() { DefaultAccess = ToolAccessLevel.ReadOnly }
                }
            },
            Keys = new EncryptionKeySet
            {
                KeyRotationDate = DateTime.UtcNow.AddDays(90)
            },
            AuditConfig = new AuditConfiguration
            {
                EnableComprehensiveLogging = true,
                ComplianceFrameworks = new[] { "enterprise" }
            }
        };

        // Enterprise demo tenant
        _tenantConfigurations["enterprise-demo"] = new TenantContext
        {
            TenantId = "enterprise-demo",
            TenantDomain = "demo.company.com",
            DisplayName = "Enterprise Demo Tenant",
            Status = TenantStatus.Active,
            Policy = new AuthenticationPolicy
            {
                RequiredAuthMethods = new[] { "oauth", "webauthn" },
                MaxSessionDuration = TimeSpan.FromHours(4),
                RequireReauthForSensitiveTools = true,
                ToolPolicies = new Dictionary<string, ToolPolicy>
                {
                    ["GetServerMetadata"] = new() { DefaultAccess = ToolAccessLevel.Denied }
                }
            },
            ComplianceFrameworks = new[] { "SOC2", "HIPAA" }
        };

        _logger.LogInformation("Initialized {TenantCount} default tenants for enterprise development",
            _tenantConfigurations.Count);
    }
}