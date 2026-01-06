using Authentication.Models;

namespace Authentication.Interfaces;

/// <summary>
/// Simple rate limiting service interface.
/// </summary>
public interface IRateLimitingService
{
    /// <summary>
    /// Checks if request is allowed.
    /// </summary>
    Task<bool> IsRequestAllowedAsync(AuthenticationRequest request);
}