using System.Security.Claims;

namespace Authentication.Interfaces;

/// <summary>
/// Manages user sessions for MCP clients and browser-based authentication.
/// Provides session creation, validation, and revocation capabilities.
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Creates a new session for an authenticated user.
    /// </summary>
    /// <param name="user">The authenticated user's claims principal</param>
    /// <returns>A unique session token for the user</returns>
    Task<string> CreateSessionAsync(ClaimsPrincipal user);
    
    /// <summary>
    /// Validates an existing session token and returns the associated user.
    /// </summary>
    /// <param name="sessionToken">The session token to validate</param>
    /// <returns>The user's claims principal if valid, null otherwise</returns>
    Task<ClaimsPrincipal?> ValidateSessionAsync(string sessionToken);
    
    /// <summary>
    /// Revokes an existing session, logging the user out.
    /// </summary>
    /// <param name="sessionToken">The session token to revoke</param>
    Task RevokeSessionAsync(string sessionToken);
    
    /// <summary>
    /// Extends the expiration time of an active session.
    /// </summary>
    /// <param name="sessionToken">The session token to extend</param>
    /// <returns>True if the session was extended, false if not found or expired</returns>
    Task<bool> ExtendSessionAsync(string sessionToken);
    
    /// <summary>
    /// Gets all active sessions for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier</param>
    /// <returns>List of active session tokens for the user</returns>
    Task<IEnumerable<string>> GetUserSessionsAsync(string userId);
    
    /// <summary>
    /// Revokes all sessions for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier</param>
    Task RevokeAllUserSessionsAsync(string userId);
}