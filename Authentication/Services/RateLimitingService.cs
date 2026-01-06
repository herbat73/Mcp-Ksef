using Authentication.Interfaces;
using Authentication.Models;
using Authentication.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Authentication.Services;

/// <summary>
/// Simple rate limiting service - MVP implementation.
/// </summary>
public class RateLimitingService : IRateLimitingService
{
    private readonly ILogger<RateLimitingService> _logger;
    private readonly AuthenticationConfiguration _authConfig;
    private readonly ConcurrentDictionary<string, List<DateTime>> _requests = new();

    public RateLimitingService(ILogger<RateLimitingService> logger, IOptions<AuthenticationConfiguration> authConfig)
    {
        _logger = logger;
        _authConfig = authConfig.Value;
    }

    /// <summary>
    /// Rate limiting using configuration settings.
    /// </summary>
    public async Task<bool> IsRequestAllowedAsync(AuthenticationRequest request)
    {
        try
        {
            var clientIp = request.ClientIPAddress;
            var now = DateTime.UtcNow;
            var oneMinuteAgo = now.AddMinutes(-1);
            
            var requestList = _requests.GetOrAdd(clientIp, _ => new List<DateTime>());
            var rateLimitConfig = _authConfig.Security.RateLimit;
            
            lock (requestList)
            {
                // Remove old requests
                requestList.RemoveAll(time => time < oneMinuteAgo);
                
                // Check if under limit using configuration
                var requestsPerMinute = rateLimitConfig.RequestsPerMinute;
                if (requestList.Count >= requestsPerMinute)
                {
                    _logger.LogWarning("Rate limit exceeded for {IP}: {Count}/{Limit} requests", 
                        clientIp, requestList.Count, requestsPerMinute);
                    return false;
                }
                
                // Record this request
                requestList.Add(now);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate limiting error - allowing request");
            return true; // Fail open for safety
        }
    }
}