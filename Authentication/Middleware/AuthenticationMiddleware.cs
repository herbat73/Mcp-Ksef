using Authentication.Interfaces;
using Authentication.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Authentication.Middleware;

/// <summary>
/// Enterprise authentication middleware for MCP server requests.
/// Integrates authentication into existing MCP request pipeline without breaking functionality.
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly IAuthenticationModeProvider _authProvider;

    public AuthenticationMiddleware(
        RequestDelegate next,
        ILogger<AuthenticationMiddleware> logger,
        IAuthenticationModeProvider authProvider)
    {
        _next = next;
        _logger = logger;
        _authProvider = authProvider;
    }

    /// <summary>
    /// Processes HTTP request with enterprise authentication validation.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Only apply authentication to MCP endpoint (root path)
            if (!IsMCPRequest(context))
            {
                await _next(context);
                return;
            }

            // Skip authentication if disabled
            if (_authProvider.CurrentMode == AuthenticationMode.Disabled)
            {
                _logger.LogDebug("Authentication disabled - processing request normally");
                await _next(context);
                return;
            }

            // Extract authentication request context
            var authRequest = await CreateAuthenticationRequestAsync(context);
            
            _logger.LogInformation("MCP authentication attempt from {IP} with token: {HasToken}", 
                context.Connection.RemoteIpAddress, !string.IsNullOrEmpty(authRequest.BearerToken));
            
            // Validate authentication
            var authResult = await _authProvider.AuthenticateAsync(authRequest);
            
            if (!authResult.IsAuthenticated)
            {
                await HandleAuthenticationFailureAsync(context, authResult);
                return;
            }

            // Set authenticated user context
            context.User = authResult.User!;
            
            // Add authentication context for enterprise auditing
            context.Items["AuthenticationContext"] = authResult.Context;
            
            _logger.LogDebug("Request authenticated for user {User} to access tool {Tool}",
                authResult.User?.Identity?.Name, authRequest.RequestedTool);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication middleware error for request {Path}", context.Request.Path);
            
            // Return generic error to prevent information disclosure
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error");
        }
    }

    /// <summary>
    /// Determines if request should be subject to authentication.
    /// </summary>
    private static bool IsMCPRequest(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        // Exclude OAuth and system endpoints from authentication
        if (path.StartsWith("/authorize") || path.StartsWith("/token") || 
            path.StartsWith("/auth/") || path.StartsWith("/.well-known") ||
            path.StartsWith("/health") || path.StartsWith("/webauthn") ||
            path.StartsWith("/info") || path.StartsWith("/logout") || 
            path.StartsWith("/revoke"))
        {
            return false;
        }
        
        // Only apply authentication to MCP requests (POST to root)
        return context.Request.Method == "POST" && context.Request.Path == "/";
    }

    /// <summary>
    /// Creates authentication request from HTTP context.
    /// </summary>
    private async Task<AuthenticationRequest> CreateAuthenticationRequestAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        string? bearerToken = null;
        
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            bearerToken = authHeader.Substring("Bearer ".Length);
        }

        // Extract requested tool from MCP request body (basic implementation)
        string requestedTool = "unknown";
        if (context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;
            
            try
            {
                var jsonDoc = JsonDocument.Parse(body);
                if (jsonDoc.RootElement.TryGetProperty("method", out var methodElement))
                {
                    requestedTool = methodElement.GetString() ?? "unknown";
                }
            }
            catch
            {
                // Ignore JSON parsing errors for tool extraction
            }
        }

        return new AuthenticationRequest
        {
            BearerToken = bearerToken,
            ClientIPAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown",
            RequestedTool = requestedTool,
            TenantId = ExtractTenantId(context),
            RequestTime = DateTime.UtcNow,
            Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
        };
    }

    /// <summary>
    /// Extracts tenant ID from request for multi-tenant support.
    /// </summary>
    private string? ExtractTenantId(HttpContext context)
    {
        // Check X-Tenant-ID header
        var tenantHeader = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tenantHeader))
        {
            return tenantHeader;
        }

        // Default tenant for single-tenant deployments
        return "default";
    }

    /// <summary>
    /// Handles authentication failure with appropriate HTTP responses.
    /// </summary>
    private async Task HandleAuthenticationFailureAsync(HttpContext context, AuthenticationResult authResult)
    {
        _logger.LogWarning("Authentication failed: {Error} for tool {Tool} from IP {IP}",
            authResult.ErrorMessage, 
            context.Items["RequestedTool"], 
            context.Connection.RemoteIpAddress);

        // Set appropriate HTTP status code
        context.Response.StatusCode = authResult.ErrorCode switch
        {
            "unauthorized" => 401,
            "forbidden" => 403,
            "invalid_token" => 401,
            "expired_token" => 401,
            _ => 401
        };

        // Add WWW-Authenticate header for 401 responses
        if (context.Response.StatusCode == 401 && !string.IsNullOrEmpty(authResult.Challenge))
        {
            context.Response.Headers["WWW-Authenticate"] = authResult.Challenge;
        }

        // Return JSON-RPC error for MCP protocol compliance
        var errorResponse = new
        {
            jsonrpc = "2.0",
            error = new
            {
                code = -32000,
                message = authResult.ErrorMessage ?? "Authentication required",
                data = new
                {
                    error_code = authResult.ErrorCode,
                    authentication_mode = _authProvider.CurrentMode.ToString()
                }
            },
            id = (string?)null
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }
}