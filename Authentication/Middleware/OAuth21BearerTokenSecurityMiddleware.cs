using Microsoft.Extensions.Logging;

namespace Authentication.Middleware;

/// <summary>
/// OAuth 2.1 compliant bearer token security middleware.
/// Prevents bearer tokens from being passed in URI query parameters per OAuth 2.1 spec.
/// </summary>
public class OAuth21BearerTokenSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OAuth21BearerTokenSecurityMiddleware> _logger;

    public OAuth21BearerTokenSecurityMiddleware(RequestDelegate next, ILogger<OAuth21BearerTokenSecurityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // OAuth 2.1 security requirement: bearer tokens must not be in query parameters
        if (HasBearerTokenInQuery(context.Request))
        {
            _logger.LogWarning("OAuth 2.1 violation: Bearer token found in query string from {IP}. Request: {Path}?{Query}",
                context.Connection.RemoteIpAddress, context.Request.Path, context.Request.QueryString);

            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""
                {
                    "error": "invalid_request",
                    "error_description": "OAuth 2.1: Bearer tokens must not be passed in URI query parameters. Use Authorization header instead.",
                    "error_uri": "https://oauth.net/2.1/"
                }
                """);
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Detects potential bearer tokens in query parameters.
    /// Looks for common patterns that might indicate token leakage.
    /// </summary>
    private static bool HasBearerTokenInQuery(HttpRequest request)
    {
        var query = request.Query;
        
        // Check for explicit token parameters (OAuth 2.1 violation)
        if (query.ContainsKey("access_token") || 
            query.ContainsKey("bearer_token") ||
            query.ContainsKey("token"))
        {
            return true;
        }

        // Check for JWT-like patterns in any query parameter (additional protection)
        foreach (var param in query)
        {
            if (param.Value.Any(value => IsLikelyJwtToken(value)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Heuristic to detect JWT-like tokens in query parameters.
    /// Prevents accidental token exposure.
    /// </summary>
    private static bool IsLikelyJwtToken(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 50)
            return false;

        // JWT tokens have 3 base64url segments separated by dots
        var segments = value.Split('.');
        if (segments.Length == 3)
        {
            // Check if all segments look like base64url (rough heuristic)
            return segments.All(segment => 
                segment.Length > 10 && 
                segment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        }

        return false;
    }
}