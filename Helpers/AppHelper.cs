using Authentication.Configuration;
using Authentication.Controllers;
using Authentication.Middleware;
using Authentication.OAuth;
using Authentication.WebAuthn;
using Authentication.Models;
using RemoteMcpKsef.Consts;

namespace RemoteMcpKsef.Helpers;

public static class AppHelper
{
    public static WebApplication Setup(WebApplicationBuilder builder)
    {
        var app = builder.Build();
        
        // Initialize AuthenticationTools with service provider for remote MCP service
        Tools.AuthenticationTools.Initialize(app.Services);

        // Enable CORS middleware
        app.UseCors();

        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("🌐 HTTP REQUEST: {Method} {Path} {QueryString}", 
                context.Request.Method, context.Request.Path, context.Request.QueryString);
            await next();
        });

        // Add session support for WebAuthn challenges
        app.UseSession();

        // Add enterprise security middleware (before MCP mapping)  
        app.UseMiddleware<RateLimitingMiddleware>();

        // Add OAuth 2.1 bearer token security middleware
        app.UseMiddleware<OAuth21BearerTokenSecurityMiddleware>();

        // Enable OAuth discovery and implementation endpoints (BEFORE authentication middleware)
        app.MapOAuthEndpoints();
        app.MapOAuthImplementationEndpoints();
        app.MapWebAuthnEndpoints();

        // Map dedicated authentication endpoints for browser-based OAuth flows
        app.MapAuthenticationEndpoints();

        // Add session validation middleware (BEFORE authentication)
        app.UseMiddleware<SessionValidationMiddleware>();

        // Use standard ASP.NET Core authentication/authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Map MCP endpoints with multi-scheme authentication (Cookie + JWT Bearer)
        // MCP clients can authenticate via session cookies created through OAuth flow
        var mcpEndpoint = app.MapMcp();

        // Apply authentication based on configuration
        var authConfig = app.Configuration.GetSection(AuthenticationConfiguration.SectionName).Get<AuthenticationConfiguration>();
        if (authConfig?.Mode != AuthenticationMode.Disabled)
        {
            mcpEndpoint.RequireAuthorization("McpAccess");
        }

        // Optional: Add a health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

        // Optional: Add server info endpoint for debugging
        app.MapGet("/info", () => Results.Json(new 
        { 
            name = AppConsts.AppName,
            version = "1.0.0",
            transport = "streamable-http",
            endpoints = new 
            {
                mcp = "/mcp",
                health = "/health",
                protected_demo = "/protected",
                oauth_auth = "/authorize"
            },
            description = AppConsts.AppDescription
        }));

        // Add protected test endpoint for OAuth 2.1 testing
        app.MapGet("/protected", () => Results.Json(new 
        {
            message = "Success! You have accessed a protected endpoint.",
            timestamp = DateTime.UtcNow,
            user = "authenticated_user",
            scope = "mcp:tools"
        })).RequireAuthorization();

        return app;
    }
}