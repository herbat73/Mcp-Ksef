using Authentication.Configuration;
using Authentication.Interfaces;
using Authentication.Services;
using Authentication.WebAuthn;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Services;
using Configuration;

namespace RemoteMcpKsef.Helpers;

public static class BuilderHelper
{
    public static WebApplicationBuilder Setup(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var services  = builder.Services;
        
        // Configure cross-platform service hosting
        services.AddWindowsService(options =>
        {
            options.ServiceName = "Remote MCP KSeF Server";
        });
        services.AddSystemd();

        // Add background service for lifecycle management
        services.AddHostedService<McpServerLifecycleService>();

        // Configure logging to stderr (MCP convention) with UTC timestamps
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            consoleLogOptions.TimestampFormat = "[yyyy-MM-dd HH:mm:ss UTC] ";
            consoleLogOptions.UseUtcTimestamp = true;
        });

        // Register MCP server with HTTP transport (Streamable HTTP)
        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        // Configure enterprise authentication services
        services.Configure<AuthenticationConfiguration>(
            builder.Configuration.GetSection(AuthenticationConfiguration.SectionName));

        // Configure server settings
        services.Configure<ServerConfiguration>(
        builder.Configuration.GetSection(ServerConfiguration.SectionName));

        // Register authentication services following Microsoft DI patterns
        // Use consistent lifetimes to avoid DI violations

        // Application Services (per-request scope for proper lifecycle)
        services.AddScoped<IAuthenticationModeProvider, AuthenticationModeProvider>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IMultiTenantTokenService, MultiTenantTokenService>();

        // Domain Layer (scoped to maintain consistency boundary)
        services.AddScoped<Authentication.Domain.Services.IAuthenticationDomainService, Authentication.Domain.Services.AuthenticationDomainService>();

        // Infrastructure Layer (scoped for database connection lifetime)
        services.AddScoped<Authentication.Domain.Repositories.IUserRepository, Authentication.Infrastructure.Repositories.InMemoryUserRepository>();

        // Enterprise Services (scoped for dependency consistency)
        services.AddScoped<IEnterpriseOAuthPolicyService, EnterpriseOAuthPolicyService>();
        services.AddScoped<IClientCertificateService, ClientCertificateService>();
        services.AddScoped<IEnterpriseWebAuthnService, EnterpriseWebAuthnService>();
        services.AddScoped<IPasswordlessAIAuthFlow, PasswordlessAIAuthFlow>();

        // OAuth endpoint providers removed for clean state

        // Register rate limiting service
        services.AddSingleton<IRateLimitingService, RateLimitingService>();

        // Register SOLID key management service
        services.AddSingleton<ICryptographicUtilityService, Authentication.Domain.Services.CryptographicUtilityService>();
        services.AddSingleton<ISigningKeyService, SigningKeyService>();

        // Register session management service for MCP and OAuth integration
        services.AddSingleton<ISessionManagementService, SessionManagementService>();

        // Register OAuth endpoint provider services
        services.AddScoped<SimpleOAuthEndpointProvider>();
        services.AddScoped<LocalOAuthEndpointProvider>();
        services.AddScoped<IOAuthEndpointProviderFactory, OAuthEndpointProviderFactory>();

        // Add multi-scheme authentication: Cookie + JWT Bearer
        services.AddAuthentication(options =>
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
        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        // Add authorization with multi-scheme policy for MCP access
        services.AddAuthorization(options =>
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
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(8); // Extend for OAuth sessions
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax; // Fix: Use Lax for localhost OAuth flow
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // Configure authentication database (in-memory for development)
        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseInMemoryDatabase("AuthDatabase");
        });

        // Add CORS for browser-based MCP clients
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return builder;
    }
}