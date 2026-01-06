using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Authentication.Interfaces;
using Authentication.Models;
using Authentication.Configuration;

namespace Authentication.Controllers;

/// <summary>
/// Handles browser-based authentication flows separate from MCP protocol endpoints.
/// Implements OAuth 2.1 with Microsoft Azure AD SSO integration.
/// </summary>
public static class AuthenticationController
{
    /// <summary>
    /// Maps authentication endpoints for browser-based OAuth flows.
    /// </summary>
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Mapping authentication endpoints for OAuth flow separation");
        
        // Browser login endpoint - initiates OAuth flow
        app.MapGet("/auth/login", async (HttpContext context,
            [FromServices] IOptionsMonitor<AuthenticationConfiguration> authConfig) =>
        {
            var authProvider = authConfig.CurrentValue.ExternalIdP.Provider;
            logger.LogInformation("Initiating login flow with provider: {Provider}", authProvider);
            
            // Check if user is already authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                logger.LogInformation("User already authenticated, redirecting to home");
                return Results.Redirect("/");
            }
            
            // Determine authentication scheme based on provider
            var authScheme = authProvider == "AzureAD" 
                ? "AzureAD"  // Will be configured in Sprint 3
                : "Local";    // Fallback to local auth
            
            // Create authentication properties for OAuth flow
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/auth/callback",
                Items =
                {
                    ["provider"] = authProvider,
                    ["returnUrl"] = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/"
                }
            };
            
            // For now, redirect to OAuth authorize endpoint
            // This will be replaced with proper Challenge in Sprint 3
            var issuer = authConfig.CurrentValue.OAuth.Issuer;
            var clientId = "mcp-remote";
            var redirectUri = Uri.EscapeDataString($"{issuer}/auth/callback");
            var state = Guid.NewGuid().ToString("N");
            
            // Store state in session for CSRF protection
            context.Session.SetString($"oauth_state_{state}", state);
            
            var authUrl = authProvider == "AzureAD"
                ? $"https://login.microsoftonline.com/{authConfig.CurrentValue.ExternalIdP.AzureAD.TenantId}/oauth2/v2.0/authorize" +
                  $"?client_id={authConfig.CurrentValue.ExternalIdP.AzureAD.ClientId}" +
                  $"&response_type=code" +
                  $"&redirect_uri={redirectUri}" +
                  $"&scope=openid%20profile%20email" +
                  $"&state={state}" +
                  $"&prompt=select_account"
                : $"{issuer}/authorize" +
                  $"?response_type=code" +
                  $"&client_id={clientId}" +
                  $"&redirect_uri={redirectUri}" +
                  $"&scope=mcp:tools" +
                  $"&state={state}";
            
            logger.LogInformation("Redirecting to OAuth provider: {Url}", authUrl);
            return Results.Redirect(authUrl);
        })
        .WithName("Login")
        .WithMetadata(new AllowAnonymousAttribute());
        
        // OAuth callback endpoint - handles provider response
        app.MapGet("/auth/callback", async (HttpContext context,
            [FromServices] ISessionManagementService sessionService,
            [FromServices] IOptionsMonitor<AuthenticationConfiguration> authConfig,
            [FromServices] ILogger<Program> logger) =>
        {
            var code = context.Request.Query["code"].FirstOrDefault();
            var state = context.Request.Query["state"].FirstOrDefault();
            var error = context.Request.Query["error"].FirstOrDefault();
            
            // Handle OAuth errors
            if (!string.IsNullOrEmpty(error))
            {
                var errorDescription = context.Request.Query["error_description"].FirstOrDefault();
                logger.LogWarning("OAuth callback error: {Error} - {Description}", error, errorDescription);
                return Results.BadRequest(new { error, error_description = errorDescription });
            }
            
            // Validate state for CSRF protection
            if (string.IsNullOrEmpty(state))
            {
                logger.LogWarning("OAuth callback missing state parameter");
                return Results.BadRequest(new { error = "invalid_request", error_description = "Missing state parameter" });
            }
            
            var expectedState = context.Session.GetString($"oauth_state_{state}");
            if (expectedState != state)
            {
                logger.LogWarning("OAuth callback state mismatch. Expected: {Expected}, Received: {Received}", 
                    expectedState, state);
                return Results.BadRequest(new { error = "invalid_state", error_description = "State parameter mismatch" });
            }
            
            // Clear state from session
            context.Session.Remove($"oauth_state_{state}");
            
            if (string.IsNullOrEmpty(code))
            {
                logger.LogWarning("OAuth callback missing authorization code");
                return Results.BadRequest(new { error = "invalid_request", error_description = "Missing authorization code" });
            }
            
            // TODO: Exchange authorization code for tokens (Sprint 3)
            // For now, create a mock authenticated user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "temp-user-id"),
                new Claim(ClaimTypes.Name, "Temporary User"),
                new Claim(ClaimTypes.Email, "temp@example.com"),
                new Claim("sub", "temp-user-id"),
                new Claim("provider", authConfig.CurrentValue.ExternalIdP.Provider)
            };
            
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            
            // Create session for the authenticated user
            var sessionToken = await sessionService.CreateSessionAsync(principal);
            logger.LogInformation("Session created for user with token: {Token}", 
                sessionToken.Substring(0, 8) + "...");
            
            // Sign in the user with cookie authentication
            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
            
            // Set session cookie for MCP access
            context.Response.Cookies.Append("mcp-session", sessionToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(8)
            });
            
            // Redirect to original URL or home
            var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            logger.LogInformation("Authentication successful, redirecting to: {ReturnUrl}", returnUrl);
            
            return Results.Redirect(returnUrl);
        })
        .WithName("OAuthCallback")
        .WithMetadata(new AllowAnonymousAttribute());
        
        // Logout endpoint
        app.MapPost("/auth/logout", async (HttpContext context,
            [FromServices] ISessionManagementService sessionService,
            [FromServices] ILogger<Program> logger) =>
        {
            var sessionToken = context.Request.Cookies["mcp-session"];
            
            if (!string.IsNullOrEmpty(sessionToken))
            {
                await sessionService.RevokeSessionAsync(sessionToken);
                logger.LogInformation("Session revoked for token: {Token}", 
                    sessionToken.Substring(0, Math.Min(8, sessionToken.Length)) + "...");
            }
            
            // Sign out from cookie authentication
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Clear session cookie
            context.Response.Cookies.Delete("mcp-session");
            
            logger.LogInformation("User logged out successfully");
            return Results.Ok(new { message = "Logged out successfully" });
        })
        .WithName("Logout")
        .RequireAuthorization();
        
        // Session status endpoint - for MCP clients to check authentication
        app.MapGet("/auth/status", async (HttpContext context,
            [FromServices] ISessionManagementService sessionService,
            [FromServices] ILogger<Program> logger) =>
        {
            // Check for session cookie
            var sessionToken = context.Request.Cookies["mcp-session"];
            
            if (string.IsNullOrEmpty(sessionToken))
            {
                // Check for Authorization header (JWT Bearer)
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    // JWT Bearer authentication is handled by middleware
                    if (context.User.Identity?.IsAuthenticated == true)
                    {
                        return Results.Ok(new
                        {
                            authenticated = true,
                            method = "jwt_bearer",
                            user = new
                            {
                                id = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                                name = context.User.FindFirst(ClaimTypes.Name)?.Value,
                                email = context.User.FindFirst(ClaimTypes.Email)?.Value
                            }
                        });
                    }
                }
                
                return Results.Ok(new { authenticated = false });
            }
            
            // Validate session
            var principal = await sessionService.ValidateSessionAsync(sessionToken);
            if (principal == null)
            {
                // Session invalid or expired
                context.Response.Cookies.Delete("mcp-session");
                return Results.Ok(new { authenticated = false, reason = "session_expired" });
            }
            
            // Extend session on activity
            await sessionService.ExtendSessionAsync(sessionToken);
            
            return Results.Ok(new
            {
                authenticated = true,
                method = "session_cookie",
                user = new
                {
                    id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    name = principal.FindFirst(ClaimTypes.Name)?.Value,
                    email = principal.FindFirst(ClaimTypes.Email)?.Value
                },
                session = new
                {
                    token = sessionToken.Substring(0, 8) + "...",
                    provider = principal.FindFirst("provider")?.Value
                }
            });
        })
        .WithName("AuthStatus")
        .WithMetadata(new AllowAnonymousAttribute());
        
        // Session refresh endpoint
        app.MapPost("/auth/refresh", async (HttpContext context,
            [FromServices] ISessionManagementService sessionService,
            [FromServices] ILogger<Program> logger) =>
        {
            var sessionToken = context.Request.Cookies["mcp-session"];
            
            if (string.IsNullOrEmpty(sessionToken))
            {
                return Results.Unauthorized();
            }
            
            var extended = await sessionService.ExtendSessionAsync(sessionToken);
            if (!extended)
            {
                context.Response.Cookies.Delete("mcp-session");
                return Results.Unauthorized();
            }
            
            logger.LogInformation("Session extended for token: {Token}", 
                sessionToken.Substring(0, 8) + "...");
            
            return Results.Ok(new { message = "Session extended successfully" });
        })
        .WithName("RefreshSession")
        .RequireAuthorization();
        
        logger.LogInformation("Authentication endpoints mapped successfully");
    }
}