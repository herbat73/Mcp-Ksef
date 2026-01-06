using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Authentication.Interfaces;

namespace Authentication.Middleware;

/// <summary>
/// Middleware to validate session tokens from cookies and populate the HttpContext.User.
/// This enables MCP clients to authenticate using session cookies from browser OAuth flows.
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;
    
    public SessionValidationMiddleware(
        RequestDelegate next,
        ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context, ISessionManagementService sessionService)
    {
        // Skip if user is already authenticated (e.g., via JWT Bearer)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }
        
        // Check for session cookie
        var sessionToken = context.Request.Cookies["mcp-session"];
        
        if (!string.IsNullOrEmpty(sessionToken))
        {
            try
            {
                // Validate session
                var principal = await sessionService.ValidateSessionAsync(sessionToken);
                
                if (principal != null)
                {
                    // Set the authenticated user
                    context.User = principal;
                    
                    // Create authentication ticket for cookie authentication
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                    };
                    
                    // Sign in the validated session as authenticated user
                    var ticket = new AuthenticationTicket(principal, authProperties, 
                        CookieAuthenticationDefaults.AuthenticationScheme);
                    
                    // Set the authentication result
                    var authResult = AuthenticateResult.Success(ticket);
                    
                    // Store in HttpContext items for later use
                    context.Items["SessionAuthenticated"] = true;
                    context.Items["SessionToken"] = sessionToken;
                    
                    _logger.LogDebug("Session validated for user: {UserId}", 
                        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                    
                    // Extend session on activity
                    _ = Task.Run(async () => await sessionService.ExtendSessionAsync(sessionToken));
                }
                else
                {
                    _logger.LogDebug("Session validation failed for token: {Token}", 
                        sessionToken.Substring(0, Math.Min(8, sessionToken.Length)) + "...");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating session token");
            }
        }
        
        await _next(context);
    }
}