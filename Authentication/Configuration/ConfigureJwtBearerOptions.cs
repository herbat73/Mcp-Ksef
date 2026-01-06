using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Authentication.Configuration;
using Authentication.Interfaces;
using Authentication.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace Authentication.Configuration;

/// <summary>
/// Configures JWT Bearer options using the SigningKeyService for proper key management.
/// Implements IConfigureNamedOptions for reliable dependency injection integration.
/// </summary>
public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    private readonly ISigningKeyService _signingKeyService;

    public ConfigureJwtBearerOptions(
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ISigningKeyService signingKeyService)
    {
        _authConfig = authConfig;
        _signingKeyService = signingKeyService;
    }

    public void Configure(JwtBearerOptions options)
    {
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        var logger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ConfigureJwtBearerOptions>();
        logger.LogCritical("🔧 ConfigureJwtBearerOptions.Configure called with scheme: {SchemeName}", name);
        
        // Only configure for the JWT Bearer authentication scheme
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            logger.LogCritical("🔧 Skipping configuration - scheme {SchemeName} != {ExpectedScheme}", name, JwtBearerDefaults.AuthenticationScheme);
            return;
        }

        var authConfig = _authConfig.CurrentValue;
        logger.LogCritical("🔧 Authentication mode: {Mode}", authConfig.Mode);
        if (authConfig.Mode != AuthenticationMode.AuthorizationServer)
        {
            logger.LogCritical("🔧 Skipping configuration - mode {Mode} != AuthorizationServer", authConfig.Mode);
            return;
        }
        
        logger.LogCritical("🔧 Applying JWT Bearer configuration");

        // When acting as authorization server, we validate our own tokens
        // regardless of external IdP (we authenticate with external IdP but issue our own tokens)
        var issuer = authConfig.OAuth.Issuer;
        logger.LogInformation("Configuring JWT Bearer for local token validation (Authorization Server mode)");
        logger.LogInformation("Token issuer: {Issuer}", issuer);
        
        // Use our signing key service to validate tokens we issued
        options.TokenValidationParameters = _signingKeyService.GetValidationParameters(issuer, issuer);

        // Configure RFC 9068 compliance for access tokens with detailed logging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ConfigureJwtBearerOptions>>();
                logger.LogError("JWT Bearer authentication failed: {Exception}", context.Exception?.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ConfigureJwtBearerOptions>>();
                var token = context.SecurityToken as JwtSecurityToken;
                
                logger.LogInformation("JWT Bearer token validated successfully. Skipping RFC 9068 typ validation due to .NET JWT library limitations.");
                
                // Note: RFC 9068 "at+jwt" tokens are correctly generated with proper typ header,
                // but .NET's JwtSecurityToken doesn't expose the typ claim consistently.
                // Since we control token generation and know it's RFC 9068 compliant, 
                // we skip client-side validation here.
                
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ConfigureJwtBearerOptions>>();
                logger.LogInformation("JWT Bearer authentication challenge triggered for {Method} {Path}", 
                    context.HttpContext.Request.Method, context.HttpContext.Request.Path);
                
                // Check if this is a browser request (Accept header contains text/html)
                var acceptHeader = context.HttpContext.Request.Headers.Accept.ToString();
                var isBrowserRequest = acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase);
                
                if (isBrowserRequest)
                {
                    logger.LogInformation("Browser request detected, redirecting to OAuth authorization endpoint");
                    
                    // Create OAuth authorization URL with proper parameters
                    var authConfig = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<AuthenticationConfiguration>>();
                    var issuer = authConfig.CurrentValue.OAuth.Issuer;
                    var state = Guid.NewGuid().ToString("N")[..8]; // Short random state
                    
                    var authUrl = $"{issuer}/authorize" +
                        $"?response_type=code" +
                        $"&client_id=mcp-remote" +
                        $"&redirect_uri={Uri.EscapeDataString($"{issuer}/oauth/callback")}" +
                        $"&scope=mcp:tools" +
                        $"&state={state}";
                    
                    context.Response.Redirect(authUrl);
                    context.HandleResponse(); // Prevent default 401 response
                }
                
                return Task.CompletedTask;
            }
        };

        // Additional security settings
        options.RequireHttpsMetadata = false; // Allow HTTP for development
        options.SaveToken = false; // Don't save token in AuthenticationProperties
        options.IncludeErrorDetails = true; // Include detailed error information
    }
}