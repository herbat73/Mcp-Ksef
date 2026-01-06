using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Authentication.Interfaces;
using Authentication.Models;
using Authentication.Configuration;

namespace Authentication.Services;

/// <summary>
/// Enterprise-grade session management service implementing secure session handling
/// for both browser-based OAuth flows and MCP client authentication.
/// </summary>
public class SessionManagementService : ISessionManagementService
{
    private readonly ILogger<SessionManagementService> _logger;
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    private readonly ICryptographicUtilityService _cryptoService;
    
    // In-memory session store - replace with Redis/distributed cache in production
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _userSessions = new();
    private readonly TimeSpan _sessionTimeout;
    private readonly TimeSpan _sessionExtension;
    
    public SessionManagementService(
        ILogger<SessionManagementService> logger,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ICryptographicUtilityService cryptoService)
    {
        _logger = logger;
        _authConfig = authConfig;
        _cryptoService = cryptoService;
        
        // Configure session timeouts from configuration
        _sessionTimeout = TimeSpan.FromHours(8); // Default 8 hour sessions
        _sessionExtension = TimeSpan.FromHours(1); // Extend by 1 hour on activity
    }
    
    public async Task<string> CreateSessionAsync(ClaimsPrincipal user)
    {
        // Extract user identifier
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? user.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User must have a unique identifier claim");
        
        var userName = user.FindFirst(ClaimTypes.Name)?.Value 
            ?? user.FindFirst("name")?.Value 
            ?? "Unknown User";
        
        // Generate cryptographically secure session token
        var sessionToken = GenerateSecureToken();
        
        // Create session data
        var sessionData = new SessionData
        {
            SessionId = sessionToken,
            UserId = userId,
            UserName = userName,
            Claims = user.Claims.Select(c => new SessionClaim 
            { 
                Type = c.Type, 
                Value = c.Value 
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_sessionTimeout),
            LastAccessedAt = DateTime.UtcNow,
            IpAddress = null, // Will be set by middleware
            UserAgent = null  // Will be set by middleware
        };
        
        // Store session
        _sessions[sessionToken] = sessionData;
        
        // Track user sessions for management
        _userSessions.AddOrUpdate(userId,
            new HashSet<string> { sessionToken },
            (key, existing) =>
            {
                existing.Add(sessionToken);
                return existing;
            });
        
        _logger.LogInformation("Session created for user {UserId} ({UserName}) with token {Token}",
            userId, userName, sessionToken.Substring(0, 8) + "...");
        
        // Clean up expired sessions periodically
        _ = Task.Run(() => CleanupExpiredSessionsAsync());
        
        return await Task.FromResult(sessionToken);
    }
    
    public async Task<ClaimsPrincipal?> ValidateSessionAsync(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }
        
        // Retrieve session
        if (!_sessions.TryGetValue(sessionToken, out var sessionData))
        {
            _logger.LogWarning("Session validation failed: Token not found");
            return null;
        }
        
        // Check expiration
        if (sessionData.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Session validation failed: Token expired for user {UserId}",
                sessionData.UserId);
            await RevokeSessionAsync(sessionToken);
            return null;
        }
        
        // Update last accessed time
        sessionData.LastAccessedAt = DateTime.UtcNow;
        
        // Reconstruct claims principal
        var claims = sessionData.Claims.Select(c => new Claim(c.Type, c.Value));
        var identity = new ClaimsIdentity(claims, "Session");
        var principal = new ClaimsPrincipal(identity);
        
        _logger.LogDebug("Session validated for user {UserId}", sessionData.UserId);
        
        return await Task.FromResult(principal);
    }
    
    public async Task RevokeSessionAsync(string sessionToken)
    {
        if (_sessions.TryRemove(sessionToken, out var sessionData))
        {
            // Remove from user sessions tracking
            if (_userSessions.TryGetValue(sessionData.UserId, out var userSessions))
            {
                userSessions.Remove(sessionToken);
                
                if (userSessions.Count == 0)
                {
                    _userSessions.TryRemove(sessionData.UserId, out _);
                }
            }
            
            _logger.LogInformation("Session revoked for user {UserId} ({UserName})",
                sessionData.UserId, sessionData.UserName);
        }
        
        await Task.CompletedTask;
    }
    
    public async Task<bool> ExtendSessionAsync(string sessionToken)
    {
        if (_sessions.TryGetValue(sessionToken, out var sessionData))
        {
            // Check if session is still valid
            if (sessionData.ExpiresAt > DateTime.UtcNow)
            {
                // Extend expiration
                sessionData.ExpiresAt = DateTime.UtcNow.Add(_sessionTimeout);
                sessionData.LastAccessedAt = DateTime.UtcNow;
                
                _logger.LogDebug("Session extended for user {UserId}", sessionData.UserId);
                return await Task.FromResult(true);
            }
        }
        
        return await Task.FromResult(false);
    }
    
    public async Task<IEnumerable<string>> GetUserSessionsAsync(string userId)
    {
        if (_userSessions.TryGetValue(userId, out var sessions))
        {
            // Return only valid (non-expired) sessions
            var validSessions = sessions
                .Where(token => _sessions.TryGetValue(token, out var data) 
                    && data.ExpiresAt > DateTime.UtcNow)
                .ToList();
            
            return await Task.FromResult(validSessions);
        }
        
        return await Task.FromResult(Enumerable.Empty<string>());
    }
    
    public async Task RevokeAllUserSessionsAsync(string userId)
    {
        if (_userSessions.TryRemove(userId, out var sessions))
        {
            foreach (var sessionToken in sessions)
            {
                _sessions.TryRemove(sessionToken, out _);
            }
            
            _logger.LogInformation("All sessions revoked for user {UserId} ({Count} sessions)",
                userId, sessions.Count);
        }
        
        await Task.CompletedTask;
    }
    
    private string GenerateSecureToken()
    {
        // Generate 32 bytes of cryptographically secure random data
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        // Convert to URL-safe base64
        return Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
    
    private async Task CleanupExpiredSessionsAsync()
    {
        try
        {
            var expiredTokens = _sessions
                .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var token in expiredTokens)
            {
                await RevokeSessionAsync(token);
            }
            
            if (expiredTokens.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredTokens.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
        }
    }
    
    /// <summary>
    /// Internal session data structure
    /// </summary>
    private class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<SessionClaim> Claims { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
    
    /// <summary>
    /// Claim storage for session reconstruction
    /// </summary>
    private class SessionClaim
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}