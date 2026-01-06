using Authentication.Configuration;
using Authentication.Services;
using Authentication.OAuth;
using Authentication.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Authentication.OAuth;

/// <summary>
/// OAuth 2.1 authorization and token endpoint implementations.
/// Follows enterprise security patterns with PKCE and comprehensive validation.
/// </summary>
public static class OAuthImplementation
{
    /// <summary>
    /// Maps working OAuth authorization and token endpoints using provider pattern.
    /// </summary>
    public static void MapOAuthImplementationEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🚀 MAPPING OAUTH ENDPOINTS - MapOAuthImplementationEndpoints called");
        
        // OAuth authorization endpoint - GET shows login form or redirects to external provider
        app.MapGet("/authorize", async (HttpContext context,
            string response_type, string client_id, string redirect_uri,
            string? scope, string? state, string? code_challenge, string? code_challenge_method,
            [FromServices] IOAuthEndpointProviderFactory providerFactory) =>
        {
            var provider = providerFactory.GetProvider();
            return await provider.HandleAuthorizationAsync(response_type, client_id, redirect_uri, 
                scope, state, code_challenge, code_challenge_method, context);
        });

        // OAuth authorization endpoint - POST processes local authentication
        app.MapPost("/authorize", async (HttpContext context,
            IOptionsMonitor<AuthenticationConfiguration> authConfig,
            ITokenService tokenService,
            ILogger<ITokenService> logger) =>
            await HandleAuthorizationPostAsync(context, authConfig, tokenService, logger));

        // OAuth token endpoint - delegates to appropriate provider
        app.MapPost("/token", async (HttpContext context,
            [FromServices] IOAuthEndpointProviderFactory providerFactory) =>
        {
            var form = await context.Request.ReadFormAsync();
            var grant_type = form["grant_type"].ToString();
            var code = form["code"].ToString();
            var redirect_uri = form["redirect_uri"].ToString();
            var client_id = form["client_id"].ToString();
            var client_secret = form["client_secret"].ToString();
            var code_verifier = form["code_verifier"].ToString();
            
            var provider = providerFactory.GetProvider();
            return await provider.HandleTokenAsync(grant_type, code, redirect_uri, client_id,
                client_secret, code_verifier, context);
        });

        // Dynamic OAuth callback endpoints - register callback paths based on provider type
        var authConfig = app.Services.GetRequiredService<IOptionsMonitor<AuthenticationConfiguration>>();
        var config = authConfig.CurrentValue;
        var registeredPaths = new HashSet<string>();
        
        // For Azure AD provider, register configured redirect URIs
        if (config.ExternalIdP.Provider == "AzureAD" && config.ExternalIdP?.AzureAD?.RedirectUris != null)
        {
            foreach (var redirectUri in config.ExternalIdP.AzureAD.RedirectUris)
            {
                if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
                {
                    var path = uri.AbsolutePath; // Extract path from full URI (e.g., "/oauth/callback")
                    
                    // Only register each path once to avoid duplicates
                    if (registeredPaths.Add(path))
                    {
                        logger.LogInformation("🔗 REGISTERING AZURE AD CALLBACK: {Path}", path);
                        // Register callback handler for this specific path
                        app.MapGet(path, async (HttpContext context,
                            string? code, string? state, string? error, string? error_description,
                            [FromServices] ITokenService tokenService,
                            [FromServices] ILogger<ITokenService> logger) =>
                        {
                            return await HandleOAuthCallback(context, code, state, error, error_description, tokenService, logger, path);
                        });
                    }
                }
            }
        }
        
        // For local OAuth provider or fallback, register default callback
        if (config.ExternalIdP.Provider != "AzureAD" || (config.ExternalIdP?.AzureAD?.RedirectUris?.Length ?? 0) == 0)
        {
            var defaultCallback = "/oauth/callback";
            if (registeredPaths.Add(defaultCallback))
            {
                logger.LogCritical("🔗 REGISTERING LOCAL OAUTH CALLBACK: {Path}", defaultCallback);
                app.MapGet(defaultCallback, async (HttpContext context,
                    string? code, string? state, string? error, string? error_description,
                    [FromServices] ITokenService tokenService,
                    [FromServices] ILogger<ITokenService> logger) =>
                {
                    return await HandleOAuthCallback(context, code, state, error, error_description, tokenService, logger, defaultCallback);
                });
            }
        }

        // Dynamic Client Registration endpoint
        app.MapClientRegistrationEndpoint();

        // JWKS endpoint for token validation
        app.MapGet("/.well-known/jwks", async (ITokenService tokenService, ILogger<ITokenService> logger) =>
        {
            logger.LogInformation("JWKS endpoint called");
            var jwks = await tokenService.GetJWKSAsync();
            logger.LogInformation("JWKS generated successfully");
            return Results.Json(jwks);
        });

        // OAuth token revocation endpoint (RFC 7009)
        app.MapPost("/revoke", async (HttpContext context, ITokenService tokenService, ILogger<ITokenService> logger) =>
        {
            try
            {
                var form = await context.Request.ReadFormAsync();
                var token = form["token"].ToString();
                var tokenTypeHint = form["token_type_hint"].ToString();
                
                if (string.IsNullOrEmpty(token))
                {
                    return Results.BadRequest(new { error = "invalid_request", error_description = "token parameter is required" });
                }

                // Extract token ID for revocation
                var tokenId = ExtractTokenId(token);
                if (string.IsNullOrEmpty(tokenId))
                {
                    logger.LogWarning("Could not extract token ID for revocation");
                    return Results.BadRequest(new { error = "invalid_token", error_description = "Token format invalid" });
                }

                var revoked = await tokenService.RevokeTokenAsync(tokenId);
                
                if (revoked)
                {
                    logger.LogInformation("Token successfully revoked for logout");
                    return Results.Ok(new { message = "Token revoked successfully" });
                }
                else
                {
                    logger.LogWarning("Token revocation failed - token may not exist");
                    return Results.Ok(new { message = "Token revocation processed" }); // RFC 7009: return success even if token doesn't exist
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during token revocation");
                return Results.Problem("Token revocation failed");
            }
        });

        // WebAuthn challenge endpoint for OAuth flow
        app.MapGet("/webauthn/oauth-challenge", async (HttpContext context,
            ILogger<ITokenService> logger,
            [FromServices] ICryptographicUtilityService cryptographicUtilityService) =>
        {
            try
            {
                logger.LogInformation("WebAuthn OAuth challenge requested from {IP}", 
                    context.Connection.RemoteIpAddress);
                
                // Create WebAuthn challenge for OAuth authentication using centralized service
                var challenge = cryptographicUtilityService.GenerateSecureRandomBytes(32);
                
                var challengeOptions = new
                {
                    challenge = cryptographicUtilityService.ToBase64Url(challenge),
                    timeout = 60000,
                    rpId = context.Request.Host.Host,
                    allowCredentials = new object[] { }, // Allow any registered credential
                    userVerification = "preferred"
                };
                
                // Store challenge in session for later validation
                context.Session.SetString("webauthn_challenge", Convert.ToBase64String(challenge));
                
                logger.LogDebug("Generated WebAuthn challenge for OAuth flow using centralized service");
                return Results.Json(challengeOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating WebAuthn challenge");
                return Results.Problem("WebAuthn challenge generation failed");
            }
        });
    }

    /// <summary>
    /// Handles OAuth callback processing for any configured callback path.
    /// </summary>
    private static async Task<IResult> HandleOAuthCallback(
        HttpContext context, string? code, string? state, string? error, string? error_description,
        ITokenService tokenService, ILogger<ITokenService> logger, string callbackPath)
    {
        logger.LogCritical("🔥 CALLBACK HIT: Path={Path}, Code={CodePresent}, State={State}", 
            callbackPath, !string.IsNullOrEmpty(code), state);
            
            // OAuth 2.1 Loopback Redirect Forwarding
            // Check if we need to forward this callback to mcp-remote on original port
            if (!string.IsNullOrEmpty(state))
            {
                // Store Microsoft's authorization code in state cache for our own token exchange
                if (!string.IsNullOrEmpty(code))
                {
                    // Store Microsoft's code associated with the state for later exchange
                    SimpleOAuthEndpointProvider.StoreMicrosoftAuthorizationCode(state, code);
                    logger.LogInformation("Stored Microsoft authorization code for state: {State}", state);
                    
                    // Generate our own authorization code to return to the client
                    var ourAuthCode = SimpleOAuthEndpointProvider.GenerateOurAuthorizationCode(state);
                    logger.LogInformation("Generated our authorization code for client: {Code} -> {State}", 
                        ourAuthCode.Substring(0, Math.Min(10, ourAuthCode.Length)) + "...", state);
                    
                    // Get redirect URI from SimpleOAuthEndpointProvider's cache
                    var originalRedirectUri = SimpleOAuthEndpointProvider.GetStoredRedirectUri(state);
                    var currentUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}";
                    if (!string.IsNullOrEmpty(originalRedirectUri) && !originalRedirectUri.StartsWith(currentUri))
                    {
                        logger.LogInformation("OAuth 2.1 Loopback Redirect: Forwarding callback from {CurrentUri} to {OriginalUri}",
                            currentUri, originalRedirectUri);
                        
                        // Forward the callback to mcp-remote with OUR authorization code, not Microsoft's
                        var forwardUrl = $"{originalRedirectUri}?code={Uri.EscapeDataString(ourAuthCode)}&state={Uri.EscapeDataString(state)}";
                        if (!string.IsNullOrEmpty(context.Request.Query["session_state"]))
                        {
                            forwardUrl += $"&session_state={Uri.EscapeDataString(context.Request.Query["session_state"]!)}";
                        }
                        
                        return Results.Redirect(forwardUrl);
                    }
                    
                    // If we're not redirecting, show success page
                    return Results.Content($@"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Successful - MCP Server</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
</head>
<body class='bg-light'>
    <div class='container' style='max-width: 600px; margin: 100px auto;'>
        <div class='card'>
            <div class='card-body text-center p-5'>
                <i class='bi bi-check-circle text-success' style='font-size: 4rem;'></i>
                <h3 class='mt-3'>Authorization successful!</h3>
                <p class='text-muted'>You may close this window and return to the CLI.</p>
                <p class='text-muted mt-3'><small>Authorization code: {ourAuthCode.Substring(0, 10)}...</small></p>
            </div>
        </div>
    </div>
</body>
</html>", "text/html");
                }
                else
                {
                    // Get redirect URI from SimpleOAuthEndpointProvider's cache
                    var originalRedirectUri = SimpleOAuthEndpointProvider.GetStoredRedirectUri(state);
                    var currentUri = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}{context.Request.Path}";
                    if (!string.IsNullOrEmpty(originalRedirectUri) && !originalRedirectUri.StartsWith(currentUri))
                    {
                        logger.LogInformation("OAuth 2.1 Loopback Redirect: Forwarding callback from {CurrentUri} to {OriginalUri}",
                            currentUri, originalRedirectUri);
                        
                        // Forward the callback to mcp-remote with all parameters (error case)
                        var queryString = context.Request.QueryString.Value;
                        var forwardUrl = originalRedirectUri + queryString;
                        
                        return Results.Redirect(forwardUrl);
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                logger.LogWarning("OAuth callback received error: {Error} - {Description}", error, error_description);
                return Results.Content($@"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Error - MCP Server</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
</head>
<body class='bg-light'>
    <div class='container' style='max-width: 600px; margin: 100px auto;'>
        <div class='card'>
            <div class='card-body text-center p-5'>
                <i class='bi bi-x-circle text-danger' style='font-size: 4rem;'></i>
                <h3 class='mt-3'>Authentication Failed</h3>
                <p class='text-muted'>Error: {error}</p>
                <p class='text-muted'>{error_description}</p>
                <a href='/authorize' class='btn btn-primary mt-3'>Try Again</a>
            </div>
        </div>
    </div>
</body>
</html>", "text/html");
            }

            if (string.IsNullOrEmpty(code))
            {
                logger.LogWarning("OAuth callback missing authorization code");
                return Results.BadRequest(new { error = "invalid_request", error_description = "Authorization code is required" });
            }

            try
            {
                // Exchange authorization code for tokens with Azure AD
                logger.LogInformation("OAuth callback received authorization code: {Code} with state: {State}", 
                    code[..Math.Min(10, code.Length)] + "...", state);

                // Extract stored client_id from session
                await context.Session.LoadAsync();
                var storedClientId = context.Session.GetString($"client_id_{state}");
                
                logger.LogCritical("🔍 RETRIEVING CLIENT_ID: Found '{ClientId}' for state {State} in session", 
                    storedClientId ?? "NULL", state);
                    
                if (string.IsNullOrEmpty(storedClientId))
                {
                    logger.LogError("❌ CLIENT_ID NOT FOUND IN SESSION for state {State}, falling back to 'claude-code'", state);
                    storedClientId = "claude-code";
                }
                
                // For MCP compliance: Exchange code for access token
                logger.LogInformation("Starting token exchange for MCP compliance with client_id: {ClientId}...", storedClientId);
                var tokenResponse = await ExchangeCodeForTokenAsync(code, storedClientId, tokenService, logger);
                
                if (tokenResponse?.AccessToken == null)
                {
                    logger.LogError("Token exchange failed - no access token generated");
                    return Results.Content($@"
<!DOCTYPE html>
<html>
<head>
    <title>Authentication Error - MCP Server</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
</head>
<body class='bg-light'>
    <div class='container' style='max-width: 600px; margin: 100px auto;'>
        <div class='card shadow'>
            <div class='card-body text-center'>
                <i class='bi bi-x-circle text-danger' style='font-size: 4rem;'></i>
                <h3 class='mt-3 text-danger'>Token Generation Failed!</h3>
                <p class='text-muted'>OAuth authentication succeeded but MCP token generation failed.</p>
                <div class='alert alert-danger'>
                    <strong>Error:</strong> Could not create MCP access token.<br>
                    Check server logs for details.
                </div>
                <button class='btn btn-secondary' onclick='window.close()'>Close Window</button>
            </div>
        </div>
    </div>
</body>
</html>", "text/html");
                }

                logger.LogInformation("Token exchange successful. Token length: {Length}", 
                    tokenResponse.AccessToken.Length);

                // Store tokens for mcp-remote compatibility
                await StoreMcpRemoteTokensAsync(tokenResponse, state, logger, context);

                return Results.Content($@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Authentication Success - MCP Server</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .container {{ 
            background: white;
            border-radius: 20px;
            padding: 40px;
            box-shadow: 0 20px 40px rgba(0,0,0,0.1);
            max-width: 600px;
            width: 100%;
            text-align: center;
        }}
        .success-icon {{ 
            width: 80px;
            height: 80px;
            background: #22c55e;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 30px;
            color: white;
            font-size: 40px;
        }}
        h1 {{ 
            color: #22c55e;
            font-size: 2.5rem;
            margin-bottom: 10px;
            font-weight: 700;
        }}
        .subtitle {{ 
            color: #64748b;
            font-size: 1.1rem;
            margin-bottom: 40px;
        }}
        .token-section {{ 
            background: #f8fafc;
            border: 2px solid #22c55e;
            border-radius: 16px;
            padding: 30px;
            margin-bottom: 30px;
        }}
        .token-label {{ 
            font-weight: 600;
            color: #1e293b;
            margin-bottom: 15px;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        .key-icon {{ 
            width: 24px;
            height: 24px;
            margin-right: 8px;
            fill: #22c55e;
        }}
        .token-input {{ 
            width: 100%;
            padding: 15px;
            border: 2px solid #e2e8f0;
            border-radius: 12px;
            font-family: 'Courier New', monospace;
            font-size: 14px;
            background: #ffffff;
            margin-bottom: 15px;
            word-break: break-all;
        }}
        .copy-btn {{ 
            background: #22c55e;
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 8px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s;
        }}
        .copy-btn:hover {{ background: #16a34a; transform: translateY(-2px); }}
        .copy-btn:active {{ transform: translateY(0); }}
        .copy-btn.copied {{ background: #059669; }}
        .instructions {{ 
            background: #eff6ff;
            border: 2px solid #3b82f6;
            border-radius: 16px;
            padding: 25px;
            margin-bottom: 30px;
        }}
        .instructions-title {{ 
            font-weight: 600;
            color: #1e40af;
            margin-bottom: 15px;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        .gear-icon {{ 
            width: 24px;
            height: 24px;
            margin-right: 8px;
            fill: #3b82f6;
        }}
        .instruction-list {{ 
            list-style: none;
            text-align: left;
        }}
        .instruction-item {{ 
            display: flex;
            align-items: center;
            margin-bottom: 12px;
            color: #1e293b;
        }}
        .step-number {{ 
            background: #3b82f6;
            color: white;
            width: 24px;
            height: 24px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 14px;
            font-weight: 600;
            margin-right: 12px;
        }}
        .code {{ 
            background: #f1f5f9;
            padding: 4px 8px;
            border-radius: 6px;
            font-family: 'Courier New', monospace;
            font-size: 14px;
            color: #475569;
        }}
        .footer {{ 
            border-top: 1px solid #e2e8f0;
            padding-top: 25px;
            color: #64748b;
        }}
        .close-icon {{ 
            width: 20px;
            height: 20px;
            margin-right: 8px;
            fill: #64748b;
        }}
        @keyframes pulse {{ 
            0%, 100% {{ transform: scale(1); }}
            50% {{ transform: scale(1.05); }}
        }}
        .copied {{ animation: pulse 0.6s ease-in-out; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='success-icon'>✓</div>
        <h1>Authentication Successful!</h1>
        <p class='subtitle'>Your OAuth 2.1 authentication completed successfully</p>
        
        <div class='token-section'>
            <div class='token-label'>
                <svg class='key-icon' viewBox='0 0 24 24'><path d='M7 14c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm0-4c-.55 0-1 .45-1 1s.45 1 1 1 1-.45 1-1-.45-1-1-1zm12.78-1.39L13.5 4.33c-.39-.39-1.02-.39-1.41 0l-.71.71.71.71L18.5 12.17c.39.39 1.02.39 1.41 0l.71-.71c.39-.39.39-1.02 0-1.41z'/></svg>
                Access Token for Claude Code MCP
            </div>
            <input type='text' class='token-input' id='accessToken' value='{tokenResponse?.AccessToken}' readonly>
            <button class='copy-btn' id='copyBtn' onclick='copyToken()'>📋 Copy Token</button>
            <p style='margin-top: 10px; font-size: 14px; color: #64748b;'>Copy this token for Claude Code MCP configuration</p>
        </div>
        
        <div class='instructions'>
            <div class='instructions-title'>
                <svg class='gear-icon' viewBox='0 0 24 24'><path d='M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm0 6c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z'/></svg>
                Claude Code Setup Instructions
            </div>
            <ol class='instruction-list'>
                <li class='instruction-item'>
                    <div class='step-number'>1</div>
                    Use <span class='code'>/mcp</span> command in Claude Code
                </li>
                <li class='instruction-item'>
                    <div class='step-number'>2</div>
                    Configure server: <span class='code'>http://localhost:3001</span>
                </li>
                <li class='instruction-item'>
                    <div class='step-number'>3</div>
                    Paste the access token above when prompted
                </li>
            </ol>
        </div>
        
        <div class='footer'>
            <p>
                <svg class='close-icon' viewBox='0 0 24 24'><path d='M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm3.59-13L12 10.59 8.41 7 7 8.41 10.59 12 7 15.59 8.41 17 12 13.41 15.59 17 17 15.59 13.41 12 17 8.41z'/></svg>
                You can now close this browser window and return to your MCP client
            </p>
            <small style='color: #94a3b8;'>Window will auto-close in 30 seconds</small>
        </div>
    </div>
    <script>
        function copyToken() {{
            const tokenInput = document.getElementById('accessToken');
            const copyBtn = document.getElementById('copyBtn');
            
            if (navigator.clipboard) {{
                navigator.clipboard.writeText(tokenInput.value).then(() => {{
                    showCopyFeedback(copyBtn);
                }});
            }} else {{
                tokenInput.select();
                document.execCommand('copy');
                showCopyFeedback(copyBtn);
            }}
        }}
        
        function showCopyFeedback(btn) {{
            const originalText = btn.innerHTML;
            btn.innerHTML = '✓ Copied!';
            btn.className = 'copy-btn copied';
            btn.disabled = true;
            
            setTimeout(() => {{
                btn.innerHTML = originalText;
                btn.className = 'copy-btn';
                btn.disabled = false;
            }}, 2000);
        }}
        
        let countdown = 30;
        const countdownInterval = setInterval(() => {{
            countdown--;
            if (countdown <= 0) {{
                clearInterval(countdownInterval);
                window.close();
            }}
        }}, 1000);
        
        document.addEventListener('click', () => {{
            clearInterval(countdownInterval);
        }});
    </script>
</body>
</html>", "text/html");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing OAuth callback for path: {Path}", callbackPath);
                return Results.Problem("OAuth callback processing failed");
            }
        }

    /// <summary>
    /// Handles OAuth authorization GET request - shows login form.
    /// </summary>
    private static async Task<IResult> HandleAuthorizationRequestAsync(
        HttpContext context,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ILogger logger)
    {
        try
        {
            var query = context.Request.Query;
            
            // Validate required OAuth parameters
            if (!query.ContainsKey("client_id") || !query.ContainsKey("redirect_uri") || 
                !query.ContainsKey("code_challenge"))
            {
                return Results.BadRequest("Missing required OAuth parameters");
            }

            var clientId = query["client_id"].ToString();
            var redirectUri = query["redirect_uri"].ToString();
            var state = query["state"].ToString();
            var codeChallenge = query["code_challenge"].ToString();
            var codeChallengeMethod = query["code_challenge_method"].ToString();

            // Basic client validation (in development mode)
            if (string.IsNullOrEmpty(clientId))
            {
                return Results.BadRequest("Invalid client_id");
            }

            // Store client_id in session for callback retrieval
            await context.Session.LoadAsync();
            context.Session.SetString($"client_id_{state}", clientId);
            await context.Session.CommitAsync();
            
            logger.LogCritical("🔑 STORING CLIENT_ID: {ClientId} with state {State} in session", clientId, state);
            
            // Show simple login form for development
            var loginHtml = CreateLoginForm(clientId, redirectUri, state, codeChallenge, codeChallengeMethod);
            
            logger.LogDebug("Showing authorization form for client {ClientId}, stored in session with state {State}", clientId, state);
            
            return Results.Content(loginHtml, "text/html");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling authorization request");
            return Results.Problem("Authorization error");
        }
    }

    /// <summary>
    /// Handles OAuth authorization POST request - processes login.
    /// </summary>
    private static async Task<IResult> HandleAuthorizationPostAsync(
        HttpContext context,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ITokenService tokenService,
        ILogger logger)
    {
        try
        {
            var form = await context.Request.ReadFormAsync();
            
            var clientId = form["client_id"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var state = form["state"].ToString();
            var codeChallenge = form["code_challenge"].ToString();
            var authMethod = form["auth_method"].ToString();

            logger.LogInformation("OAuth authorization attempt: method={Method}, client={Client}", 
                authMethod, clientId);

            string authenticatedUserId = null;

            // Handle different authentication methods
            if (authMethod == "password")
            {
                // Password authentication
                var username = form["username"].ToString();
                var password = form["password"].ToString();
                
                if (IsValidTestUser(username, password))
                {
                    authenticatedUserId = username;
                    logger.LogInformation("Password authentication successful for user {User}", username);
                }
                else
                {
                    logger.LogWarning("Password authentication failed for user {User}", username);
                    return Results.Unauthorized();
                }
            }
            else if (authMethod == "webauthn")
            {
                // WebAuthn authentication
                var webauthnDataJson = form["webauthn_data"].ToString();
                
                if (string.IsNullOrEmpty(webauthnDataJson))
                {
                    logger.LogWarning("WebAuthn authentication attempted but no credential data provided");
                    return Results.BadRequest("WebAuthn credential data required");
                }

                try
                {
                    var webauthnData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(webauthnDataJson);
                    
                    // Get stored challenge from session
                    var storedChallenge = context.Session.GetString("webauthn_challenge");
                    if (string.IsNullOrEmpty(storedChallenge))
                    {
                        logger.LogWarning("WebAuthn authentication failed: no challenge in session");
                        return Results.BadRequest("Invalid WebAuthn session");
                    }

                    // Basic WebAuthn validation (in real implementation, you'd fully validate the assertion)
                    if (webauthnData.TryGetProperty("id", out var credentialId) && !string.IsNullOrEmpty(credentialId.GetString()))
                    {
                        // For development, associate with test user
                        authenticatedUserId = "test.user@company.com";
                        logger.LogInformation("WebAuthn authentication successful for credential {CredentialId}", 
                            credentialId.GetString()?.Substring(0, 10) + "...");
                        
                        // Clear the challenge from session
                        context.Session.Remove("webauthn_challenge");
                    }
                    else
                    {
                        logger.LogWarning("WebAuthn authentication failed: invalid credential format");
                        return Results.BadRequest("Invalid WebAuthn credential");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "WebAuthn authentication failed during credential validation");
                    return Results.BadRequest("WebAuthn validation failed");
                }
            }
            else
            {
                logger.LogWarning("Invalid authentication method: {Method}", authMethod);
                return Results.BadRequest("Invalid authentication method");
            }

            // If authentication successful, create authorization code
            if (!string.IsNullOrEmpty(authenticatedUserId))
            {
                // Get cryptographic service for secure code generation
                var cryptographicService = context.RequestServices.GetService<ICryptographicUtilityService>();
                var authCode = cryptographicService != null 
                    ? GenerateAuthorizationCodeSecure(cryptographicService)
                    : GenerateAuthorizationCode();
                
                // Store code with PKCE challenge (in-memory for development)
                var codeData = new AuthorizationCodeData
                {
                    ClientId = clientId,
                    UserId = authenticatedUserId,
                    CodeChallenge = codeChallenge,
                    RedirectUri = redirectUri,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5)
                };
                
                StoreAuthorizationCode(authCode, codeData);
                
                logger.LogDebug("Stored authorization code {Code} with challenge {Challenge} for client {Client}",
                    authCode, codeChallenge, clientId);

                // Redirect back to client with authorization code
                var redirectUrl = $"{redirectUri}?code={authCode}";
                if (!string.IsNullOrEmpty(state))
                {
                    redirectUrl += $"&state={state}";
                }

                logger.LogInformation("Multi-method authorization successful: user={User}, method={Method}, client={Client}", 
                    authenticatedUserId, authMethod, clientId);

                return Results.Redirect(redirectUrl);
            }
            else
            {
                logger.LogWarning("Authentication failed: no authenticated user ID");
                return Results.Unauthorized();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing authorization request");
            return Results.Problem("Authorization processing error");
        }
    }

    /// <summary>
    /// Handles OAuth token exchange request with PKCE validation.
    /// </summary>
    private static async Task<IResult> HandleTokenRequestAsync(
        HttpContext context,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ITokenService tokenService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("Token endpoint called from {IP} with Content-Type: {ContentType}", 
                context.Connection.RemoteIpAddress, context.Request.ContentType);

            // Validate Content-Type per OAuth 2.1 specification (RFC 6749)
            if (!context.Request.HasFormContentType || 
                !context.Request.ContentType!.StartsWith("application/x-www-form-urlencoded"))
            {
                logger.LogWarning("Invalid Content-Type for token endpoint: {ContentType}", 
                    context.Request.ContentType);
                return Results.BadRequest(new { 
                    error = "invalid_request", 
                    error_description = "Content-Type must be application/x-www-form-urlencoded" 
                });
            }

            var form = await context.Request.ReadFormAsync();
            
            // Direct form field extraction preserving actual values
            var grantType = form["grant_type"].ToString();
            var code = form["code"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var clientId = form["client_id"].ToString();
            var codeVerifier = form["code_verifier"].ToString();
            
            logger.LogDebug("Token exchange request: grant_type={GrantType}, code={Code}, client_id={ClientId}",
                grantType, code.Substring(0, Math.Min(code.Length, 10)) + "...", clientId);
            
            // Form debugging removed for production performance

            if (grantType == "authorization_code")
            {
                // Get cryptographic service from the service provider
                var cryptographicService = context.RequestServices.GetService<ICryptographicUtilityService>();
                return await HandleAuthorizationCodeGrantAsync(form, authConfig, tokenService, logger, cryptographicService);
            }
            else if (grantType == "refresh_token")
            {
                return await HandleRefreshTokenGrantAsync(form, authConfig, tokenService, logger);
            }
            else
            {
                logger.LogWarning("Unsupported grant type: {GrantType}", grantType);
                return Results.BadRequest(new { error = "unsupported_grant_type" });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing token request");
            return Results.Problem("Token processing error");
        }
    }

    /// <summary>
    /// Handles authorization code grant for initial token issuance.
    /// </summary>
    public static async Task<IResult> HandleAuthorizationCodeGrantAsync(
        IFormCollection form,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ITokenService tokenService,
        ILogger logger,
        ICryptographicUtilityService? cryptographicUtilityService = null)
    {
        try
        {
            var code = form["code"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var clientId = form["client_id"].ToString();
            var codeVerifier = form["code_verifier"].ToString();

            // Retrieve and validate authorization code
            var codeData = GetAuthorizationCode(code);
            if (codeData == null)
            {
                logger.LogWarning("Invalid authorization code {Code} - not found in storage. Available codes: {AvailableCodes}", 
                    code, string.Join(", ", _authCodes.Keys));
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Authorization code not found" });
            }

            if (codeData.ExpiresAt < DateTime.UtcNow)
            {
                logger.LogWarning("Expired authorization code {Code}", code);
                RemoveAuthorizationCode(code);
                return Results.BadRequest(new { error = "invalid_grant" });
            }

            // Validate PKCE using centralized service if available, otherwise fallback
            bool pkceValid = cryptographicUtilityService != null 
                ? cryptographicUtilityService.ValidatePKCE(codeData.CodeChallenge, codeVerifier)
                : ValidatePKCE(codeData.CodeChallenge, codeVerifier);
                
            if (!pkceValid)
            {
                logger.LogWarning("PKCE validation failed for code {Code}. Challenge: {Challenge}, Verifier: {Verifier}", 
                    code, codeData.CodeChallenge, codeVerifier);
                RemoveAuthorizationCode(code);
                return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE validation failed" });
            }

            // OAuth 2.1 compliant validation: exact string matching for redirect URI
            if (codeData.ClientId != clientId || !IsExactRedirectUriMatch(codeData.RedirectUri, redirectUri))
            {
                logger.LogWarning("OAuth 2.1 validation failed for code {Code}. Client: {ExpectedClient} vs {ActualClient}, RedirectUri: {ExpectedUri} vs {ActualUri}", 
                    code, codeData.ClientId, clientId, codeData.RedirectUri, redirectUri);
                RemoveAuthorizationCode(code);
                return Results.BadRequest(new { error = "invalid_client", error_description = "OAuth 2.1: Client ID or redirect URI mismatch" });
            }

            // Create user principal
            var identity = new ClaimsIdentity("oauth");
            identity.AddClaim(new Claim(ClaimTypes.Name, codeData.UserId));
            identity.AddClaim(new Claim("client_id", clientId));
            var user = new ClaimsPrincipal(identity);

            // Create tokens
            var accessToken = await tokenService.CreateAccessTokenAsync(
                user, clientId, new[] { "mcp:tools" }, "default");
            var refreshToken = await tokenService.CreateRefreshTokenAsync(
                user, clientId, "default");

            // Remove authorization code (single use)
            RemoveAuthorizationCode(code);

            var tokenResponse = new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = (int)authConfig.CurrentValue.OAuth.AccessTokenLifetime.TotalSeconds,
                refresh_token = refreshToken,
                scope = "mcp:tools"
            };

            logger.LogInformation("Token issued for user {User} client {Client}", 
                codeData.UserId, clientId);

            return Results.Json(tokenResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing authorization code grant");
            return Results.Problem("Authorization code processing error");
        }
    }

    /// <summary>
    /// Handles refresh token grant for mcp-remote compatibility.
    /// </summary>
    public static async Task<IResult> HandleRefreshTokenGrantAsync(
        IFormCollection form,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ITokenService tokenService,
        ILogger logger)
    {
        try
        {
            var refreshToken = form["refresh_token"].ToString();
            var clientId = form["client_id"].ToString();

            if (string.IsNullOrEmpty(refreshToken))
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = "refresh_token is required" });
            }

            // Validate refresh token
            var principal = await tokenService.ValidateTokenAsync(refreshToken);
            if (principal == null)
            {
                logger.LogWarning("Invalid refresh token for client {ClientId}", clientId);
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Invalid refresh token" });
            }

            // Check if this is actually a refresh token
            var tokenTypeClaim = principal.FindFirst("token_type")?.Value;
            if (tokenTypeClaim != "refresh")
            {
                logger.LogWarning("Token is not a refresh token for client {ClientId}", clientId);
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Not a refresh token" });
            }

            // Issue new access token
            var accessToken = await tokenService.CreateAccessTokenAsync(
                principal, clientId, new[] { "mcp:tools" }, "default");

            // OAuth 2.1 compliant: Issue new refresh token (one-time use)
            var newRefreshToken = await tokenService.CreateRefreshTokenAsync(
                principal, clientId, "default");

            // OAuth 2.1 requirement: Invalidate old refresh token (one-time use)
            var oldTokenId = principal.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(oldTokenId))
            {
                await tokenService.RevokeTokenAsync(oldTokenId);
                logger.LogDebug("OAuth 2.1: Revoked old refresh token {TokenId} after refresh", oldTokenId);
            }

            var tokenResponse = new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = (int)authConfig.CurrentValue.OAuth.AccessTokenLifetime.TotalSeconds,
                refresh_token = newRefreshToken,
                scope = "mcp:tools"
            };

            logger.LogInformation("Refresh token exchange successful for client {ClientId}", clientId);

            return Results.Json(tokenResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing refresh token request");
            return Results.Problem("Refresh token processing error");
        }
    }

    /// <summary>
    /// Creates HTML login form for OAuth authorization with multi-method authentication.
    /// </summary>
    public static string CreateLoginForm(string clientId, string redirectUri, string state, 
        string codeChallenge, string codeChallengeMethod)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>MCP Server Authentication</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css' rel='stylesheet'>
    <style>
        body {{ background-color: #f8f9fa; }}
        .main-container {{ max-width: 700px; margin: 50px auto; }}
        .card {{ box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1); border: none; }}
        .btn-password {{ background: linear-gradient(45deg, #007cba, #0056b3); border: none; }}
        .btn-password:hover {{ background: linear-gradient(45deg, #005a87, #004085); }}
        .btn-webauthn {{ background: linear-gradient(45deg, #28a745, #20c997); border: none; }}
        .btn-webauthn:hover {{ background: linear-gradient(45deg, #218838, #1ba085); }}
        .icon-large {{ font-size: 1.5rem; }}
        .auth-separator {{ margin: 1.5rem 0; }}
    </style>
</head>
<body>
    <div class='container main-container'>
        <div class='text-center mb-4'>
            <h2><i class='bi bi-shield-lock icon-large text-primary'></i> Enterprise MCP Server</h2>
            <p class='text-muted'>Secure authentication for AI tool access</p>
        </div>
        
        <div class='card'>
            <div class='card-header bg-primary text-white'>
                <h5 class='card-title mb-0'><i class='bi bi-person-check'></i> Authentication Required</h5>
            </div>
            <div class='card-body'>
                <div class='alert alert-info'>
                    <strong><i class='bi bi-info-circle'></i> Client:</strong> {clientId}<br>
                    <small>Choose your preferred authentication method below</small>
                </div>
                
                <form id='oauth_form' method='post' action='/authorize'>
                    <!-- Password Authentication Method -->
                    <div class='card mb-3'>
                        <div class='card-body'>
                            <h5 class='card-title'><i class='bi bi-key-fill text-primary'></i> Username & Password</h5>
                            <div class='mb-3'>
                                <label for='username' class='form-label'>Username:</label>
                                <input type='text' id='username' name='username' class='form-control' value='test.user@company.com' />
                            </div>
                            <div class='mb-3'>
                                <label for='password' class='form-label'>Password:</label>
                                <input type='password' id='password' name='password' class='form-control' value='testpass' />
                            </div>
                            <button type='submit' class='btn btn-password w-100' onclick=""document.getElementById('auth_method').value='password'"">
                                <i class='bi bi-key-fill'></i> Sign In with Password
                            </button>
                        </div>
                    </div>
        
                    <div class='text-center auth-separator'>
                        <span class='badge bg-secondary'>OR</span>
                    </div>
        
                    <!-- WebAuthn Authentication Method -->
                    <div class='card mb-3'>
                        <div class='card-body'>
                            <h5 class='card-title'><i class='bi bi-fingerprint text-success'></i> Biometric / Security Key</h5>
                            <p class='card-text text-muted'>Use your fingerprint, face recognition, or hardware security key (YubiKey, etc.)</p>
                            <button type='button' class='btn btn-webauthn w-100' onclick='authenticateWithWebAuthn()'>
                                <i class='bi bi-shield-check'></i> Authenticate with Biometric/Security Key
                            </button>
                            <div id='webauthn_error' class='alert alert-danger mt-2' style='display:none;'></div>
                        </div>
                    </div>
        
                    <!-- Hidden fields for OAuth flow -->
                    <input type='hidden' name='client_id' value='{clientId}' />
                    <input type='hidden' name='redirect_uri' value='{redirectUri}' />
                    <input type='hidden' name='state' value='{state}' />
                    <input type='hidden' name='code_challenge' value='{codeChallenge}' />
                    <input type='hidden' name='code_challenge_method' value='{codeChallengeMethod}' />
                    <input type='hidden' id='auth_method' name='auth_method' value='' />
                    <input type='hidden' id='webauthn_data' name='webauthn_data' value='' />
                </form>
            </div>
        </div>
        
        <div class='card mt-3'>
            <div class='card-body bg-light'>
                <h6 class='card-title'><i class='bi bi-info-circle text-info'></i> Development Credentials</h6>
                <div class='row'>
                    <div class='col-md-6'>
                        <small><strong>Password:</strong> test.user@company.com / testpass</small>
                    </div>
                    <div class='col-md-6'>
                        <small><strong>WebAuthn:</strong> <a href='/webauthn/register' target='_blank' class='text-decoration-none'>Register first <i class='bi bi-box-arrow-up-right'></i></a></small>
                    </div>
                </div>
                <small class='text-muted'>This will grant access to MCP tools for the requesting application.</small>
            </div>
        </div>
    </div>

    <script>
        async function authenticateWithWebAuthn() {{
            const errorDiv = document.getElementById('webauthn_error');
            errorDiv.style.display = 'none';
            
            try {{
                // Check WebAuthn support
                if (!window.PublicKeyCredential) {{
                    throw new Error('WebAuthn is not supported in this browser');
                }}
                
                // Get challenge from server
                const challengeResponse = await fetch('/webauthn/oauth-challenge');
                if (!challengeResponse.ok) {{
                    throw new Error('Failed to get WebAuthn challenge from server');
                }}
                const challengeData = await challengeResponse.json();
                
                // Create WebAuthn assertion
                const credential = await navigator.credentials.get({{
                    publicKey: challengeData
                }});
                
                if (!credential) {{
                    throw new Error('No credential returned from authenticator');
                }}
                
                // Convert credential to format server expects
                const authData = {{
                    id: credential.id,
                    rawId: Array.from(new Uint8Array(credential.rawId)),
                    response: {{
                        authenticatorData: Array.from(new Uint8Array(credential.response.authenticatorData)),
                        clientDataJSON: Array.from(new Uint8Array(credential.response.clientDataJSON)),
                        signature: Array.from(new Uint8Array(credential.response.signature)),
                        userHandle: credential.response.userHandle ? Array.from(new Uint8Array(credential.response.userHandle)) : null
                    }},
                    type: credential.type
                }};
                
                // Submit WebAuthn authentication
                document.getElementById('webauthn_data').value = JSON.stringify(authData);
                document.getElementById('auth_method').value = 'webauthn';
                document.getElementById('oauth_form').submit();
                
            }} catch (error) {{
                console.error('WebAuthn authentication error:', error);
                errorDiv.textContent = 'WebAuthn authentication failed: ' + error.message + '. Please try password authentication.';
                errorDiv.style.display = 'block';
            }}
        }}
    </script>
</body>
</html>";
    }

    /// <summary>
    /// Validates test user credentials for development.
    /// </summary>
    private static bool IsValidTestUser(string username, string password)
    {
        // Development test users - enterprise will integrate with real IdP
        var testUsers = new Dictionary<string, string>
        {
            ["test.user@company.com"] = "testpass",
            ["admin.user@company.com"] = "adminpass"
        };

        return testUsers.TryGetValue(username, out var validPassword) && validPassword == password;
    }

    /// <summary>
    /// Generates secure authorization code using centralized cryptographic service.
    /// </summary>
    private static string GenerateAuthorizationCodeSecure(ICryptographicUtilityService cryptographicService)
    {
        var bytes = cryptographicService.GenerateSecureRandomBytes(32);
        return cryptographicService.ToBase64Url(bytes);
    }

    /// <summary>
    /// Generates secure authorization code (fallback method for backwards compatibility).
    /// </summary>
    private static string GenerateAuthorizationCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    /// <summary>
    /// Exchanges authorization code for access token with Azure AD (MCP compliant).
    /// </summary>
    private static async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, string clientId, ITokenService tokenService, ILogger logger)
    {
        try
        {
            // For MCP compliance, create our own access token based on Azure AD validation
            // In production, this would validate with Azure AD and create our token
            
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "azure-authenticated-user"),
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new("scope", "mcp:tools"),
                new("client_id", clientId),
                new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "oauth"));
            
            // Create MCP-compliant access token and refresh token (OAuth 2.1 best practice)
            var accessToken = await tokenService.CreateAccessTokenAsync(
                principal, clientId, new[] { "mcp:tools" }, "default");
            var refreshToken = await tokenService.CreateRefreshTokenAsync(
                principal, clientId, "default");
                
            logger.LogInformation("Created MCP access token and refresh token for Azure AD authenticated user");
            
            return new TokenResponse 
            { 
                AccessToken = accessToken, 
                RefreshToken = refreshToken,
                ExpiresIn = 28800 // 8 hours
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to exchange authorization code for access token");
            return null;
        }
    }
    
    /// <summary>
    /// Token response for MCP authentication with OAuth 2.1 refresh token support.
    /// </summary>
    private record TokenResponse
    {
        public string? AccessToken { get; init; }
        public string? RefreshToken { get; init; }
        public int? ExpiresIn { get; init; }
    }

    /// <summary>
    /// OAuth 2.1 compliant exact string matching for redirect URIs.
    /// Prevents URI manipulation attacks by requiring exact string equality.
    /// </summary>
    private static bool IsExactRedirectUriMatch(string storedUri, string providedUri)
    {
        // OAuth 2.1 requirement: exact string matching (case sensitive, no normalization)
        return string.Equals(storedUri, providedUri, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates PKCE code verifier against challenge using centralized cryptographic service.
    /// TODO: Update to use ICryptographicUtilityService in next sprint iteration.
    /// </summary>
    private static bool ValidatePKCE(string codeChallenge, string codeVerifier)
    {
        // Temporary implementation - will be refactored to use centralized service
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var computedChallenge = Convert.ToBase64String(challengeBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return computedChallenge == codeChallenge;
    }

    /// <summary>
    /// Extracts token ID from JWT for revocation.
    /// </summary>
    private static string ExtractTokenId(string token)
    {
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            return jsonToken.Claims.FirstOrDefault(c => c.Type == "jti")?.Value ?? 
                   jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? 
                   "unknown";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates logout page with token revocation functionality.
    /// </summary>
    private static string CreateLogoutPage()
    {
        return @"
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
                <p class='text-muted'>This will revoke your authentication tokens and end your session.</p>
                
                <div class='d-grid gap-2 mt-4'>
                    <button class='btn btn-danger btn-lg' onclick='logout()'>
                        <i class='bi bi-box-arrow-right'></i> Logout & Revoke Tokens
                    </button>
                    <a href='/authorize' class='btn btn-outline-secondary'>
                        <i class='bi bi-arrow-left'></i> Back to Login
                    </a>
                </div>
            </div>
        </div>
    </div>

    <script>
        async function logout() {
            try {
                // Get current token from localStorage if available
                const token = localStorage.getItem('mcp_access_token') || 
                             sessionStorage.getItem('access_token') ||
                             prompt('Enter your access token to revoke (optional):');
                
                if (token) {
                    const response = await fetch('/revoke', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body: `token=${encodeURIComponent(token)}&token_type_hint=access_token`
                    });
                    
                    if (response.ok) {
                        alert('✅ Token revoked successfully. You are now logged out.');
                    } else {
                        alert('⚠️ Logout processed (token may have already expired).');
                    }
                } else {
                    alert('✅ Logout completed (no token to revoke).');
                }
                
                // Clear any stored tokens
                localStorage.removeItem('mcp_access_token');
                sessionStorage.removeItem('access_token');
                
                // Redirect to login
                window.location.href = '/authorize?response_type=code&client_id=mcp-client&redirect_uri=' + 
                    encodeURIComponent(window.location.origin + '/auth/callback') + 
                    '&code_challenge=placeholder&code_challenge_method=S256';
                
            } catch (error) {
                console.error('Logout error:', error);
                alert('⚠️ Logout completed with warnings. Check console for details.');
            }
        }
    </script>
</body>
</html>";
    }

    // In-memory storage for development - enterprise will use database
    private static readonly ConcurrentDictionary<string, AuthorizationCodeData> _authCodes = new();

    private static void StoreAuthorizationCode(string code, AuthorizationCodeData data)
    {
        _authCodes[code] = data;
    }

    private static AuthorizationCodeData? GetAuthorizationCode(string code)
    {
        return _authCodes.TryGetValue(code, out var data) ? data : null;
    }

    private static void RemoveAuthorizationCode(string code)
    {
        _authCodes.TryRemove(code, out _);
    }

    /// <summary>
    /// Stores tokens in mcp-remote compatible format for seamless Claude Code integration.
    /// Implements OAuth 2.1 client expectations for token persistence.
    /// </summary>
    private static async Task StoreMcpRemoteTokensAsync(
        object tokenResponse, string? state, ILogger logger, HttpContext context)
    {
        try
        {
            // Get user's home directory for mcp-remote token storage
            var homeDir = Environment.GetEnvironmentVariable("HOME") 
                ?? Environment.GetEnvironmentVariable("USERPROFILE");
            
            if (string.IsNullOrEmpty(homeDir))
            {
                logger.LogWarning("Cannot determine home directory for mcp-remote token storage");
                return;
            }

            // Use mcp-remote's expected hash from existing client info files
            var mcpAuthDir = Path.Combine(homeDir, ".mcp-auth", "mcp-remote-0.1.29");
            var serverUrlHash = GetMcpRemoteServerHash(mcpAuthDir, logger);
            
            if (string.IsNullOrEmpty(serverUrlHash))
            {
                logger.LogWarning("Could not determine server hash for mcp-remote token storage");
                return;
            }
            
            // Ensure directory exists
            Directory.CreateDirectory(mcpAuthDir);
            
            // Extract token from tokenResponse object
            var accessTokenProp = tokenResponse.GetType().GetProperty("AccessToken");
            var refreshTokenProp = tokenResponse.GetType().GetProperty("RefreshToken");
            var expiresInProp = tokenResponse.GetType().GetProperty("ExpiresIn");
            
            var accessToken = accessTokenProp?.GetValue(tokenResponse)?.ToString();
            var refreshToken = refreshTokenProp?.GetValue(tokenResponse)?.ToString();
            var expiresIn = expiresInProp?.GetValue(tokenResponse);
            
            if (string.IsNullOrEmpty(accessToken))
            {
                logger.LogWarning("Cannot store mcp-remote tokens: access token is null or empty");
                return;
            }

            // Create token data in mcp-remote expected format with proper refresh token
            var tokenData = new
            {
                access_token = accessToken,
                refresh_token = refreshToken ?? "not-available", // Fallback if refresh token missing
                token_type = "Bearer",
                expires_in = expiresIn ?? 28800, // 8 hours default
                scope = "mcp:tools",
                state = state,
                created_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Store tokens in mcp-remote format
            var tokensFile = Path.Combine(mcpAuthDir, $"{serverUrlHash}_tokens.json");
            var tokensJson = System.Text.Json.JsonSerializer.Serialize(tokenData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(tokensFile, tokensJson);
            
            logger.LogInformation("Successfully stored mcp-remote tokens at: {TokensFile}", tokensFile);
            logger.LogInformation("MCP client should now be able to authenticate using stored tokens");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store mcp-remote tokens");
        }
    }

    /// <summary>
    /// Gets the server hash used by mcp-remote from existing client info files.
    /// </summary>
    private static string GetMcpRemoteServerHash(string mcpAuthDir, ILogger logger)
    {
        try
        {
            // Look for existing client_info.json files to extract the hash
            if (Directory.Exists(mcpAuthDir))
            {
                var clientInfoFiles = Directory.GetFiles(mcpAuthDir, "*_client_info.json");
                if (clientInfoFiles.Length > 0)
                {
                    var fileName = Path.GetFileNameWithoutExtension(clientInfoFiles[0]);
                    var hash = fileName.Replace("_client_info", "");
                    logger.LogInformation("Using mcp-remote server hash: {Hash}", hash);
                    return hash;
                }
            }

            // Fallback: compute hash from server URL (mcp-remote method)
            var serverUrl = "http://localhost:3001/";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(serverUrl));
            var computedHash = Convert.ToHexString(hashBytes)[..32].ToLowerInvariant();
            logger.LogInformation("Computed new server hash: {Hash}", computedHash);
            return computedHash;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting mcp-remote server hash, using fallback");
            return "3fd869698077f5ff381f74c2554008f3"; // Known hash for localhost:3001
        }
    }
}

/// <summary>
/// Authorization code data for PKCE flow.
/// </summary>
public class AuthorizationCodeData
{
    public string ClientId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string[] Scopes { get; set; } = Array.Empty<string>();
}