using Authentication.Configuration;
using Authentication.Controllers;
using Authentication.Interfaces;
using Authentication.Services;
using Authentication.Middleware;
using Authentication.OAuth;
using Authentication.WebAuthn;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Authentication.Models;
using Services;
using Configuration;
using RemoteMcpKsef.Helpers;

// Handle CLI commands for service management
if (args.Length > 0)
{
    await ArgHelper.Resolve(args);
}

var builder = WebApplication.CreateBuilder(args);

// Configure cross-platform service hosting
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Remote MCP KSeF Server";
});

builder.Services.AddSystemd();

// Add background service for lifecycle management
builder.Services.AddHostedService<McpServerLifecycleService>();

// Configure logging to stderr (MCP convention) with UTC timestamps
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    consoleLogOptions.TimestampFormat = "[yyyy-MM-dd HH:mm:ss UTC] ";
    consoleLogOptions.UseUtcTimestamp = true;
});

// Register MCP server with HTTP transport (Streamable HTTP)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Configure enterprise authentication services
builder.Services.Configure<AuthenticationConfiguration>(
    builder.Configuration.GetSection(AuthenticationConfiguration.SectionName));

// Configure server settings
builder.Services.Configure<ServerConfiguration>(
    builder.Configuration.GetSection(ServerConfiguration.SectionName));

// Register authentication services following Microsoft DI patterns
// Use consistent lifetimes to avoid DI violations

// Application Services (per-request scope for proper lifecycle)
builder.Services.AddScoped<IAuthenticationModeProvider, AuthenticationModeProvider>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IMultiTenantTokenService, MultiTenantTokenService>();

// Domain Layer (scoped to maintain consistency boundary)
builder.Services.AddScoped<Authentication.Domain.Services.IAuthenticationDomainService, Authentication.Domain.Services.AuthenticationDomainService>();

// Infrastructure Layer (scoped for database connection lifetime)
builder.Services.AddScoped<Authentication.Domain.Repositories.IUserRepository, Authentication.Infrastructure.Repositories.InMemoryUserRepository>();

// Enterprise Services (scoped for dependency consistency)
builder.Services.AddScoped<IEnterpriseOAuthPolicyService, EnterpriseOAuthPolicyService>();
builder.Services.AddScoped<IClientCertificateService, ClientCertificateService>();
builder.Services.AddScoped<IEnterpriseWebAuthnService, EnterpriseWebAuthnService>();
builder.Services.AddScoped<IPasswordlessAIAuthFlow, PasswordlessAIAuthFlow>();

// OAuth endpoint providers removed for clean state

// Register rate limiting service
builder.Services.AddSingleton<IRateLimitingService, RateLimitingService>();

// Register SOLID key management service
builder.Services.AddSingleton<ICryptographicUtilityService, Authentication.Domain.Services.CryptographicUtilityService>();
builder.Services.AddSingleton<ISigningKeyService, SigningKeyService>();

// Register session management service for MCP and OAuth integration
builder.Services.AddSingleton<ISessionManagementService, SessionManagementService>();

// Register OAuth endpoint provider services
builder.Services.AddScoped<SimpleOAuthEndpointProvider>();
builder.Services.AddScoped<LocalOAuthEndpointProvider>();
builder.Services.AddScoped<IOAuthEndpointProviderFactory, OAuthEndpointProviderFactory>();

// Add multi-scheme authentication: Cookie + JWT Bearer
builder.Services.AddAuthentication(options =>
    {
        // Set default scheme to Cookie for browser requests
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "mcp-session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Configuration will be completed by ConfigureJwtBearerOptions below
    });

// Configure JWT Bearer options using proper IConfigureNamedOptions pattern
builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();

// Add authorization with multi-scheme policy for MCP access
builder.Services.AddAuthorization(options =>
{
    // Create composite policy supporting both Cookie and JWT Bearer authentication
    options.AddPolicy("McpAccess", policy =>
    {
        policy.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
        policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
    });
    
    // Keep default policy for other endpoints
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add session support for OAuth and WebAuthn  
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // Extend for OAuth sessions
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // Fix: Use Lax for localhost OAuth flow
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Configure authentication database (in-memory for development)
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseInMemoryDatabase("AuthDatabase");
});

// Add CORS for browser-based MCP clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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
    name = "Remote MCP KSeF Server",
    version = "1.0.0",
    transport = "streamable-http",
    endpoints = new 
    {
        mcp = "/mcp",
        health = "/health",
        protected_demo = "/protected",
        oauth_auth = "/authorize"
    },
    description = "Remote MCP server for KSeF"
}));

// Add protected test endpoint for OAuth 2.1 testing
app.MapGet("/protected", () => Results.Json(new 
{
    message = "Success! You have accessed a protected endpoint.",
    timestamp = DateTime.UtcNow,
    user = "authenticated_user",
    scope = "mcp:tools"
})).RequireAuthorization();

// Start server - use configuration from appsettings.json
var serverConfig = app.Configuration.GetSection(ServerConfiguration.SectionName).Get<ServerConfiguration>() ?? new ServerConfiguration();
var serverUrl = serverConfig.GetUrl();
app.Run(serverUrl);
return;



// Make Program class accessible for testing  
public partial class Program { }

/// <summary>
/// MCP Tools - All classes marked with [McpServerToolType] are automatically registered
/// Tool implementations are located in the Tools/ directory for better organization
/// </summary>