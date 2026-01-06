using Authentication.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Authentication.OAuth;

/// <summary>
/// OAuth 2.1 endpoint implementations following MCP authorization specification.
/// Implements Protected Resource Metadata (RFC9728) and Authorization Server Metadata (RFC8414).
/// </summary>
public static class OAuthEndpoints
{
    /// <summary>
    /// Maps OAuth 2.1 discovery and authorization endpoints to the application.
    /// </summary>
    public static void MapOAuthEndpoints(this WebApplication app)
    {
        var authConfig = app.Services.GetRequiredService<IOptionsMonitor<AuthenticationConfiguration>>();

        // OAuth 2.0 Protected Resource Metadata (RFC9728) - Required by MCP spec
        app.MapGet("/.well-known/oauth-protected-resource", 
            (HttpContext context) => GetProtectedResourceMetadata(context, authConfig));

        // OAuth 2.0 Authorization Server Metadata (RFC8414) - Required by MCP spec  
        app.MapGet("/.well-known/oauth-authorization-server",
            (HttpContext context) => GetAuthorizationServerMetadata(context, authConfig));

        // User logout endpoint  
        app.MapGet("/logout", async (HttpContext context) =>
        {
            var logoutHtml = @"
<!DOCTYPE html>
<html>
<head>
    <title>Logout - MCP Server</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css' rel='stylesheet'>
</head>
<body class='bg-light'>
    <div class='container' style='max-width: 600px; margin: 100px auto;'>
        <div class='card text-center'>
            <div class='card-body p-5'>
                <i class='bi bi-shield-x text-warning' style='font-size: 4rem;'></i>
                <h3 class='mt-3'>Logout from MCP Server</h3>
                <p class='text-muted'>Visit this page to invalidate your authentication session.</p>
                
                <div class='alert alert-success'>
                    <i class='bi bi-check-circle'></i> Session cleared successfully
                </div>
                
                <div class='d-grid gap-2 mt-4'>
                    <a href='/authorize' class='btn btn-primary btn-lg'>
                        <i class='bi bi-arrow-left'></i> Return to Login
                    </a>
                </div>
            </div>
        </div>
    </div>
</body>
</html>";
            return Results.Content(logoutHtml, "text/html");
        });

        // OpenID Connect Discovery 1.0 compatibility
        app.MapGet("/.well-known/openid-configuration",
            (HttpContext context) => GetOpenIDConfiguration(context, authConfig));

        // OAuth endpoint stubs removed - implemented in OAuthImplementation.cs
    }

    /// <summary>
    /// Returns Protected Resource Metadata as required by MCP specification (RFC9728).
    /// </summary>
    private static IResult GetProtectedResourceMetadata(
        HttpContext context, 
        IOptionsMonitor<AuthenticationConfiguration> authConfig)
    {
        var config = authConfig.CurrentValue;
        var baseUrl = GetBaseUrl(context);

        var metadata = new
        {
            resource = baseUrl,
            authorization_servers = new[] { baseUrl },
            scopes_supported = new[] { "mcp:tools", "mcp:math", "mcp:utility", "mcp:data", "mcp:reflection" },
            bearer_methods_supported = new[] { "header" },
            resource_documentation = $"{baseUrl}/info"
        };

        return Results.Json(metadata);
    }

    /// <summary>
    /// Returns Authorization Server Metadata as required by OAuth 2.1 (RFC8414).
    /// </summary>
    private static IResult GetAuthorizationServerMetadata(
        HttpContext context,
        IOptionsMonitor<AuthenticationConfiguration> authConfig)
    {
        var config = authConfig.CurrentValue;
        var baseUrl = GetBaseUrl(context);

        var metadata = new
        {
            issuer = baseUrl,
            authorization_endpoint = $"{baseUrl}/authorize",
            token_endpoint = $"{baseUrl}/token",
            registration_endpoint = config.OAuth.EnableDynamicClientRegistration ? $"{baseUrl}/register" : null,
            jwks_uri = $"{baseUrl}/.well-known/jwks",
            
            // OAuth 2.1 required parameters
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            
            // MCP-specific scopes
            scopes_supported = new[] { "mcp:tools", "mcp:math", "mcp:utility", "mcp:data", "mcp:reflection" },
            
            // Enterprise authentication methods
            token_endpoint_auth_methods_supported = config.OAuth.RequireClientCertificates 
                ? new[] { "client_secret_post", "tls_client_auth" }
                : new[] { "client_secret_post", "none" },
            
            // Enterprise security features
            require_signed_request_object = false,
            require_pushed_authorization_requests = false,
            
            // Enterprise policy information
            service_documentation = $"{baseUrl}/info",
            ui_locales_supported = new[] { "en-US" },
            
            // Enterprise compliance
            claims_supported = new[] { "sub", "aud", "iss", "exp", "iat", "tenant", "roles", "tools" }
        };

        return Results.Json(metadata);
    }

    /// <summary>
    /// Returns OpenID Connect Discovery metadata for compatibility.
    /// </summary>
    private static IResult GetOpenIDConfiguration(
        HttpContext context,
        IOptionsMonitor<AuthenticationConfiguration> authConfig)
    {
        var config = authConfig.CurrentValue;
        var baseUrl = GetBaseUrl(context);

        var metadata = new
        {
            issuer = baseUrl,
            authorization_endpoint = $"{baseUrl}/authorize",
            token_endpoint = $"{baseUrl}/token",
            userinfo_endpoint = $"{baseUrl}/userinfo",
            registration_endpoint = config.OAuth.EnableDynamicClientRegistration ? $"{baseUrl}/register" : null,
            jwks_uri = $"{baseUrl}/.well-known/jwks",
            
            // OpenID Connect required
            response_types_supported = new[] { "code" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { config.OAuth.Signing.Algorithm },
            
            // OAuth 2.1 compatibility
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            
            // Enterprise claims
            claims_supported = new[] { "sub", "aud", "iss", "exp", "iat", "tenant", "roles", "tools", "email", "name" },
            
            // Enterprise scopes
            scopes_supported = new[] { "openid", "profile", "email", "mcp:tools" }
        };

        return Results.Json(metadata);
    }

    /// <summary>
    /// Extracts base URL for metadata generation.
    /// </summary>
    private static string GetBaseUrl(HttpContext context)
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host.ToString();
        return $"{scheme}://{host}";
    }
}

/// <summary>
/// OAuth 2.1 models for enterprise authentication.
/// </summary>
public static class OAuthModels
{
    /// <summary>
    /// Client registration request model for dynamic client registration.
    /// </summary>
    public class ClientRegistrationRequest
    {
        public string ClientName { get; set; } = string.Empty;
        public string[] RedirectUris { get; set; } = Array.Empty<string>();
        public string[] GrantTypes { get; set; } = { "authorization_code" };
        public string[] ResponseTypes { get; set; } = { "code" };
        public string TokenEndpointAuthMethod { get; set; } = "none";
        public string[] Scope { get; set; } = { "mcp:tools" };
    }

    /// <summary>
    /// Client registration response model.
    /// </summary>
    public class ClientRegistrationResponse
    {
        public string ClientId { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public long ClientIdIssuedAt { get; set; }
        public long? ClientSecretExpiresAt { get; set; }
        public string[] RedirectUris { get; set; } = Array.Empty<string>();
        public string[] GrantTypes { get; set; } = Array.Empty<string>();
        public string[] ResponseTypes { get; set; } = Array.Empty<string>();
        public string ClientName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Authorization request model for OAuth flow.
    /// </summary>
    public class AuthorizationRequest
    {
        public string ResponseType { get; set; } = "code";
        public string ClientId { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string? Scope { get; set; }
        public string? State { get; set; }
        public string? CodeChallenge { get; set; }
        public string? CodeChallengeMethod { get; set; }
        public string? Resource { get; set; } // RFC8707 Resource Indicators
    }

    /// <summary>
    /// Token request model for OAuth code exchange.
    /// </summary>
    public class TokenRequest
    {
        public string GrantType { get; set; } = "authorization_code";
        public string Code { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public string? CodeVerifier { get; set; } // PKCE
        public string? Resource { get; set; } // RFC8707 Resource Indicators
    }

    /// <summary>
    /// Token response model for OAuth token issuance.
    /// </summary>
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
        public string? Scope { get; set; }
    }
}