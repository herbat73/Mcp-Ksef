using Authentication.Models;
using Authentication.WebAuthn;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Authentication.Services;

/// <summary>
/// Passwordless AI authentication flow for enterprise AI tool access.
/// Integrates WebAuthn with AI-specific authentication requirements per enterprise plan.
/// </summary>
public interface IPasswordlessAIAuthFlow
{
    /// <summary>
    /// Authenticates user for specific AI tool access using WebAuthn.
    /// </summary>
    /// <param name="toolName">AI tool being accessed</param>
    /// <param name="credentialId">WebAuthn credential identifier</param>
    /// <param name="context">AI-specific authentication context</param>
    /// <returns>Authentication result with AI-scoped permissions</returns>
    Task<AIAuthResult> AuthenticateForToolAsync(string toolName, string credentialId, AIAuthContext context);

    /// <summary>
    /// Validates tool sensitivity requirements for authentication level.
    /// </summary>
    /// <param name="toolName">Tool to validate</param>
    /// <param name="currentAuthLevel">Current authentication level</param>
    /// <returns>Required authentication actions, if any</returns>
    Task<AuthenticationRequirement[]> ValidateToolSensitivityAsync(string toolName, AuthenticationLevel currentAuthLevel);

    /// <summary>
    /// Creates correlated AI session for cross-tool interactions.
    /// </summary>
    /// <param name="user">Authenticated user</param>
    /// <param name="context">AI authentication context</param>
    /// <returns>AI session with correlation tracking</returns>
    Task<AISession> CreateAISessionAsync(ClaimsPrincipal user, AIAuthContext context);

    /// <summary>
    /// Gets AI authentication statistics for enterprise monitoring.
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <returns>AI authentication usage statistics</returns>
    Task<AIAuthStatistics> GetAIAuthStatisticsAsync(string tenantId);
}

/// <summary>
/// Passwordless AI authentication flow implementation.
/// </summary>
public class PasswordlessAIAuthFlow : IPasswordlessAIAuthFlow
{
    private readonly ILogger<PasswordlessAIAuthFlow> _logger;
    private readonly IEnterpriseWebAuthnService _webAuthnService;
    private readonly ITokenService _tokenService;
    
    // AI session tracking for enterprise monitoring
    private readonly Dictionary<string, AISession> _aiSessions = new();
    
    // Tool sensitivity configuration for enterprise security
    private readonly Dictionary<string, ToolSensitivityLevel> _toolSensitivity = new();

    public PasswordlessAIAuthFlow(
        ILogger<PasswordlessAIAuthFlow> logger,
        IEnterpriseWebAuthnService webAuthnService,
        ITokenService tokenService)
    {
        _logger = logger;
        _webAuthnService = webAuthnService;
        _tokenService = tokenService;
        
        InitializeToolSensitivity();
    }

    /// <summary>
    /// Authenticates user for specific AI tool using passwordless WebAuthn.
    /// </summary>
    public async Task<AIAuthResult> AuthenticateForToolAsync(
        string toolName, string credentialId, AIAuthContext context)
    {
        try
        {
            _logger.LogDebug("Passwordless AI authentication for tool {Tool} in tenant {Tenant}",
                toolName, context.TenantId);

            // Check tool sensitivity requirements
            var sensitivityLevel = GetToolSensitivity(toolName);
            var authRequirements = await ValidateToolSensitivityAsync(toolName, context.CurrentAuthLevel);

            if (authRequirements.Length > 0)
            {
                _logger.LogWarning("Tool {Tool} requires additional authentication: {Requirements}",
                    toolName, string.Join(", ", authRequirements.Select(r => r.ToString())));
                
                return new AIAuthResult
                {
                    IsAuthenticated = false,
                    RequiredActions = authRequirements,
                    ErrorMessage = "Additional authentication required for AI tool access"
                };
            }

            // For development, simulate WebAuthn validation
            // Full implementation would validate the actual WebAuthn credential
            var user = CreateAIUser(credentialId, context);
            
            // Create AI-specific session
            var aiSession = await CreateAISessionAsync(user, context);
            
            // Issue AI-scoped token
            var aiToken = await IssueAIScopedTokenAsync(user, toolName, context);

            _logger.LogInformation("Passwordless AI authentication successful for user {User} accessing tool {Tool}",
                user.Identity?.Name, toolName);

            return new AIAuthResult
            {
                IsAuthenticated = true,
                User = user,
                AIToken = aiToken,
                Session = aiSession,
                ToolPermissions = GetToolPermissions(toolName, sensitivityLevel),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15) // AI session timeout
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in passwordless AI authentication for tool {Tool}", toolName);
            return new AIAuthResult
            {
                IsAuthenticated = false,
                ErrorMessage = "AI authentication error"
            };
        }
    }

    /// <summary>
    /// Validates tool sensitivity for authentication requirements.
    /// </summary>
    public async Task<AuthenticationRequirement[]> ValidateToolSensitivityAsync(
        string toolName, AuthenticationLevel currentAuthLevel)
    {
        var requirements = new List<AuthenticationRequirement>();
        var sensitivityLevel = GetToolSensitivity(toolName);

        // Check if current authentication level is sufficient
        switch (sensitivityLevel)
        {
            case ToolSensitivityLevel.Public:
                // No additional authentication required
                break;
                
            case ToolSensitivityLevel.Internal:
                if (currentAuthLevel < AuthenticationLevel.Basic)
                {
                    requirements.Add(AuthenticationRequirement.BasicAuthentication);
                }
                break;
                
            case ToolSensitivityLevel.Confidential:
                if (currentAuthLevel < AuthenticationLevel.Strong)
                {
                    requirements.Add(AuthenticationRequirement.StrongAuthentication);
                }
                break;
                
            case ToolSensitivityLevel.Restricted:
                if (currentAuthLevel < AuthenticationLevel.Elevated)
                {
                    requirements.Add(AuthenticationRequirement.ElevatedAuthentication);
                    requirements.Add(AuthenticationRequirement.BiometricVerification);
                }
                break;
                
            case ToolSensitivityLevel.TopSecret:
                // Always require fresh authentication for top secret tools
                requirements.Add(AuthenticationRequirement.FreshAuthentication);
                requirements.Add(AuthenticationRequirement.BiometricVerification);
                requirements.Add(AuthenticationRequirement.SupervisorApproval);
                break;
        }

        _logger.LogDebug("Tool {Tool} sensitivity {Sensitivity} requires {RequirementCount} additional auth steps",
            toolName, sensitivityLevel, requirements.Count);

        return requirements.ToArray();
    }

    /// <summary>
    /// Creates correlated AI session for enterprise monitoring.
    /// </summary>
    public async Task<AISession> CreateAISessionAsync(ClaimsPrincipal user, AIAuthContext context)
    {
        var session = new AISession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = user.Identity?.Name ?? "unknown",
            TenantId = context.TenantId,
            RequestingApplication = context.RequestingApplication,
            SessionCorrelationId = context.SessionCorrelationId ?? Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30), // AI session timeout
            AuthenticationMethod = "webauthn_passwordless",
            SecurityLevel = context.SecurityLevel,
            Location = context.Location
        };

        _aiSessions[session.SessionId] = session;

        _logger.LogInformation("Created AI session {SessionId} for user {User} in tenant {Tenant}",
            session.SessionId, session.UserId, session.TenantId);

        return session;
    }

    /// <summary>
    /// Gets AI authentication statistics for enterprise monitoring.
    /// </summary>
    public async Task<AIAuthStatistics> GetAIAuthStatisticsAsync(string tenantId)
    {
        var tenantSessions = _aiSessions.Values.Where(s => s.TenantId == tenantId).ToArray();
        var activeSessions = tenantSessions.Where(s => s.ExpiresAt > DateTime.UtcNow).ToArray();

        return new AIAuthStatistics
        {
            TenantId = tenantId,
            ActiveAISessions = activeSessions.Length,
            TotalSessionsToday = tenantSessions.Count(s => s.CreatedAt.Date == DateTime.UtcNow.Date),
            PasswordlessAuthentications = tenantSessions.Count(s => s.AuthenticationMethod.Contains("webauthn")),
            ToolAccessEvents = tenantSessions.Sum(s => s.ToolAccessCount),
            LastAIAuthentication = tenantSessions.Max(s => s.CreatedAt)
        };
    }

    /// <summary>
    /// Gets tool sensitivity level for authentication decisions.
    /// </summary>
    private ToolSensitivityLevel GetToolSensitivity(string toolName)
    {
        if (_toolSensitivity.TryGetValue(toolName, out var level))
        {
            return level;
        }

        // Default sensitivity based on tool category
        return toolName switch
        {
            var tool when tool.Contains("Reflection") => ToolSensitivityLevel.Confidential,
            var tool when tool.Contains("Server") => ToolSensitivityLevel.Restricted,
            var tool when tool.Contains("Math") => ToolSensitivityLevel.Public,
            var tool when tool.Contains("Utility") => ToolSensitivityLevel.Internal,
            var tool when tool.Contains("Data") => ToolSensitivityLevel.Internal,
            _ => ToolSensitivityLevel.Internal
        };
    }

    /// <summary>
    /// Creates AI user principal from WebAuthn credential.
    /// </summary>
    private ClaimsPrincipal CreateAIUser(string credentialId, AIAuthContext context)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, context.UserId),
            new Claim(ClaimTypes.NameIdentifier, context.UserId),
            new Claim("tenant", context.TenantId),
            new Claim("auth_method", "webauthn_ai"),
            new Claim("credential_id", credentialId),
            new Claim("session_correlation", context.SessionCorrelationId ?? Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "webauthn_ai");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Issues AI-scoped token for tool access.
    /// </summary>
    private async Task<string> IssueAIScopedTokenAsync(ClaimsPrincipal user, string toolName, AIAuthContext context)
    {
        // Create AI-specific scopes
        var aiScopes = new[] { "mcp:ai_tools", $"mcp:tool:{toolName.ToLower()}" };
        
        return await _tokenService.CreateAccessTokenAsync(user, "ai-client", aiScopes, context.TenantId);
    }

    /// <summary>
    /// Gets tool permissions for AI context.
    /// </summary>
    private AIToolPermissions GetToolPermissions(string toolName, ToolSensitivityLevel sensitivity)
    {
        return new AIToolPermissions
        {
            ToolName = toolName,
            AccessLevel = sensitivity switch
            {
                ToolSensitivityLevel.Public => ToolAccessLevel.FullAccess,
                ToolSensitivityLevel.Internal => ToolAccessLevel.FullAccess,
                ToolSensitivityLevel.Confidential => ToolAccessLevel.LimitedWrite,
                ToolSensitivityLevel.Restricted => ToolAccessLevel.ReadOnly,
                ToolSensitivityLevel.TopSecret => ToolAccessLevel.SupervisorApproval,
                _ => ToolAccessLevel.ReadOnly
            },
            SensitivityLevel = sensitivity,
            RequiresAudit = sensitivity >= ToolSensitivityLevel.Confidential,
            SessionTimeout = sensitivity switch
            {
                ToolSensitivityLevel.Restricted => TimeSpan.FromMinutes(5),
                ToolSensitivityLevel.TopSecret => TimeSpan.FromMinutes(2),
                _ => TimeSpan.FromMinutes(15)
            }
        };
    }

    /// <summary>
    /// Initializes tool sensitivity mappings for enterprise security.
    /// </summary>
    private void InitializeToolSensitivity()
    {
        // Math tools - generally safe for AI
        _toolSensitivity["Add"] = ToolSensitivityLevel.Public;
        _toolSensitivity["Subtract"] = ToolSensitivityLevel.Public;
        _toolSensitivity["Multiply"] = ToolSensitivityLevel.Public;
        _toolSensitivity["Divide"] = ToolSensitivityLevel.Public;

        // Utility tools - internal use
        _toolSensitivity["Echo"] = ToolSensitivityLevel.Internal;
        _toolSensitivity["GetCurrentTime"] = ToolSensitivityLevel.Internal;
        _toolSensitivity["GenerateRandomNumber"] = ToolSensitivityLevel.Internal;

        // Data tools - internal use with potential data exposure
        _toolSensitivity["FormatJson"] = ToolSensitivityLevel.Internal;
        _toolSensitivity["ToUpperCase"] = ToolSensitivityLevel.Internal;
        _toolSensitivity["ToLowerCase"] = ToolSensitivityLevel.Internal;
        _toolSensitivity["ReverseText"] = ToolSensitivityLevel.Internal;

        // Reflection tools - highly sensitive system information
        _toolSensitivity["ListAllTools"] = ToolSensitivityLevel.Confidential;
        _toolSensitivity["GetToolInfo"] = ToolSensitivityLevel.Confidential;
        _toolSensitivity["ListToolsByCategory"] = ToolSensitivityLevel.Confidential;
        _toolSensitivity["SearchTools"] = ToolSensitivityLevel.Confidential;
        _toolSensitivity["GetServerMetadata"] = ToolSensitivityLevel.Restricted; // Most sensitive

        _logger.LogInformation("Initialized tool sensitivity mappings for {ToolCount} tools", _toolSensitivity.Count);
    }
}

/// <summary>
/// AI authentication context for enterprise security decisions.
/// </summary>
public class AIAuthContext
{
    /// <summary>
    /// User requesting AI tool access.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant context for multi-tenant security.
    /// </summary>
    public string TenantId { get; set; } = "default";

    /// <summary>
    /// Application requesting AI tool access.
    /// </summary>
    public string RequestingApplication { get; set; } = string.Empty;

    /// <summary>
    /// Tool category being accessed.
    /// </summary>
    public string ToolCategory { get; set; } = string.Empty;

    /// <summary>
    /// Current authentication level of the user.
    /// </summary>
    public AuthenticationLevel CurrentAuthLevel { get; set; } = AuthenticationLevel.None;

    /// <summary>
    /// Security level required for this request.
    /// </summary>
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.Standard;

    /// <summary>
    /// Session correlation ID for tracking related AI interactions.
    /// </summary>
    public string? SessionCorrelationId { get; set; }

    /// <summary>
    /// Geographic location information for location-based security.
    /// </summary>
    public GeolocationInfo? Location { get; set; }

    /// <summary>
    /// Device information for device-based security policies.
    /// </summary>
    public DeviceInfo? Device { get; set; }

    /// <summary>
    /// Request timestamp for temporal security analysis.
    /// </summary>
    public DateTime RequestTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// AI authentication result with enterprise context.
/// </summary>
public class AIAuthResult
{
    /// <summary>
    /// Indicates if AI authentication was successful.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Authenticated user principal for AI tools.
    /// </summary>
    public ClaimsPrincipal? User { get; set; }

    /// <summary>
    /// AI-scoped access token for tool operations.
    /// </summary>
    public string? AIToken { get; set; }

    /// <summary>
    /// AI session for correlation tracking.
    /// </summary>
    public AISession? Session { get; set; }

    /// <summary>
    /// Tool-specific permissions for this authentication.
    /// </summary>
    public AIToolPermissions? ToolPermissions { get; set; }

    /// <summary>
    /// Additional authentication actions required, if any.
    /// </summary>
    public AuthenticationRequirement[] RequiredActions { get; set; } = Array.Empty<AuthenticationRequirement>();

    /// <summary>
    /// Authentication expiration for AI session management.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// AI session for enterprise correlation and monitoring.
/// </summary>
public class AISession
{
    /// <summary>
    /// Unique AI session identifier.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID for this AI session.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID for multi-tenant isolation.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Application that initiated the AI session.
    /// </summary>
    public string RequestingApplication { get; set; } = string.Empty;

    /// <summary>
    /// Session correlation ID for cross-tool tracking.
    /// </summary>
    public string SessionCorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// AI session creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// AI session expiration timestamp.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Authentication method used for this AI session.
    /// </summary>
    public string AuthenticationMethod { get; set; } = string.Empty;

    /// <summary>
    /// Security level for this AI session.
    /// </summary>
    public SecurityLevel SecurityLevel { get; set; }

    /// <summary>
    /// Geographic location for location-based security.
    /// </summary>
    public GeolocationInfo? Location { get; set; }

    /// <summary>
    /// Number of tool access events in this session.
    /// </summary>
    public int ToolAccessCount { get; set; } = 0;

    /// <summary>
    /// Last tool access timestamp.
    /// </summary>
    public DateTime LastToolAccess { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// AI tool permissions with sensitivity-based controls.
/// </summary>
public class AIToolPermissions
{
    /// <summary>
    /// Tool name these permissions apply to.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Access level granted for this tool.
    /// </summary>
    public ToolAccessLevel AccessLevel { get; set; }

    /// <summary>
    /// Tool sensitivity level for security decisions.
    /// </summary>
    public ToolSensitivityLevel SensitivityLevel { get; set; }

    /// <summary>
    /// Whether this tool access requires comprehensive audit logging.
    /// </summary>
    public bool RequiresAudit { get; set; }

    /// <summary>
    /// Session timeout for this specific tool access.
    /// </summary>
    public TimeSpan SessionTimeout { get; set; }

    /// <summary>
    /// Additional constraints for this tool access.
    /// </summary>
    public string[] Constraints { get; set; } = Array.Empty<string>();
}

/// <summary>
/// AI authentication statistics for enterprise monitoring.
/// </summary>
public class AIAuthStatistics
{
    /// <summary>
    /// Tenant these statistics apply to.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Number of active AI sessions.
    /// </summary>
    public int ActiveAISessions { get; set; }

    /// <summary>
    /// Total AI sessions created today.
    /// </summary>
    public int TotalSessionsToday { get; set; }

    /// <summary>
    /// Number of passwordless authentications.
    /// </summary>
    public int PasswordlessAuthentications { get; set; }

    /// <summary>
    /// Total AI tool access events.
    /// </summary>
    public int ToolAccessEvents { get; set; }

    /// <summary>
    /// Last AI authentication timestamp.
    /// </summary>
    public DateTime LastAIAuthentication { get; set; }
}

/// <summary>
/// Supporting enums for AI authentication.
/// </summary>

public enum ToolSensitivityLevel
{
    Public,        // No restrictions
    Internal,      // Basic authentication required
    Confidential,  // Strong authentication required
    Restricted,    // Elevated authentication + audit
    TopSecret      // Maximum security controls
}

public enum AuthenticationLevel
{
    None,          // No authentication
    Basic,         // Username/password
    Strong,        // MFA or WebAuthn
    Elevated,      // Fresh WebAuthn + additional verification
    Maximum        // All security controls active
}

public enum SecurityLevel
{
    Low,
    Standard,
    High,
    Critical,
    Maximum
}

public enum AuthenticationRequirement
{
    BasicAuthentication,
    StrongAuthentication,
    ElevatedAuthentication,
    FreshAuthentication,
    BiometricVerification,
    SupervisorApproval,
    DeviceCompliance,
    LocationVerification
}

public class GeolocationInfo
{
    public string Country { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public bool IsKnownLocation { get; set; }
}