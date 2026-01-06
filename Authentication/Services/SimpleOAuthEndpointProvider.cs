using Authentication.Configuration;
using Authentication.Interfaces;
using Microsoft.Extensions.Options;

namespace Authentication.Services;

/// <summary>
/// Simple OAuth endpoint provider for Azure AD integration.
/// Implements basic authorization flow with Microsoft redirect.
/// </summary>
public class SimpleOAuthEndpointProvider : IOAuthEndpointProvider
{
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    private readonly ILogger<SimpleOAuthEndpointProvider> _logger;
    
    // Static memory cache for OAuth flow state (stateless approach for non-browser clients)
    private static readonly Dictionary<string, OAuthFlowState> _stateCache = new();
    private static readonly Dictionary<string, string> _codeToStateCache = new();
    private static readonly object _cacheLock = new();

    public SimpleOAuthEndpointProvider(
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ILogger<SimpleOAuthEndpointProvider> logger)
    {
        _authConfig = authConfig;
        _logger = logger;
    }
    
    private class OAuthFlowState
    {
        public string State { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string CodeChallenge { get; set; } = string.Empty;
        public string CodeChallengeMethod { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string CodeVerifier { get; set; } = string.Empty;
        public string MicrosoftAuthCode { get; set; } = string.Empty; // Microsoft's authorization code
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Stores Microsoft's authorization code for a given state.
    /// </summary>
    public static void StoreMicrosoftAuthorizationCode(string state, string microsoftCode)
    {
        lock (_cacheLock)
        {
            if (_stateCache.TryGetValue(state, out var flowState))
            {
                flowState.MicrosoftAuthCode = microsoftCode;
            }
        }
    }
    
    /// <summary>
    /// Generates our own authorization code and maps it to the state.
    /// </summary>
    public static string GenerateOurAuthorizationCode(string state)
    {
        var ourCode = $"mcp_{Guid.NewGuid():N}";
        lock (_cacheLock)
        {
            _codeToStateCache[ourCode] = state;
        }
        return ourCode;
    }
    
    /// <summary>
    /// Stores the authorization code to state mapping after Microsoft callback.
    /// </summary>
    public static void StoreCodeToStateMapping(string code, string state)
    {
        lock (_cacheLock)
        {
            _codeToStateCache[code] = state;
        }
    }
    
    /// <summary>
    /// Gets the stored redirect URI for a given state.
    /// </summary>
    public static string GetStoredRedirectUri(string state)
    {
        lock (_cacheLock)
        {
            if (_stateCache.TryGetValue(state, out var flowState))
            {
                return flowState.RedirectUri;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Handles OAuth authorization by storing client's PKCE and redirecting to Microsoft with our own PKCE.
    /// </summary>
    public Task<IResult> HandleAuthorizationAsync(
        string responseType, string clientId, string redirectUri,
        string? scope, string? state, string? codeChallenge, string? codeChallengeMethod,
        HttpContext context)
    {
        // CRITICAL DEBUG: Log that authorization endpoint is being hit
        var logger = context.RequestServices.GetRequiredService<ILogger<SimpleOAuthEndpointProvider>>();
        logger.LogCritical("🔑 AUTHORIZATION ENDPOINT HIT: ClientId={ClientId}, State={State}, RedirectUri={RedirectUri}", 
            clientId, state, redirectUri);

        var config = _authConfig.CurrentValue;

        // Build Microsoft OAuth authorization URL using AzureAD configuration
        // Use proper hexagonal pattern - only handle if this is the configured provider
        if (config.ExternalIdP.Provider != "AzureAD")
        {
            return Task.FromResult(Results.BadRequest(new { error = "azure_ad_not_configured" }));
        }

        var authority = config.ExternalIdP.AzureAD.Authority;
        var azureClientId = config.ExternalIdP.AzureAD.ClientId;
        
        // OAuth 2.1 Loopback Redirect URI Flexibility Implementation
        // Accept any localhost port but use registered URI for Azure AD
        var registeredRedirectUri = DetermineRegisteredRedirectUri(redirectUri, config);
        
        // Generate our own PKCE for Microsoft authentication
        var ourCodeVerifier = GeneratePKCECodeVerifier();
        var ourCodeChallenge = GeneratePKCECodeChallenge(ourCodeVerifier);
        
        // Store all flow state in memory cache (stateless for non-browser clients)
        var flowState = new OAuthFlowState
        {
            State = state ?? "",
            ClientId = clientId,
            CodeChallenge = codeChallenge ?? "",
            CodeChallengeMethod = codeChallengeMethod ?? "S256",
            RedirectUri = redirectUri,
            CodeVerifier = ourCodeVerifier
        };
        
        lock (_cacheLock)
        {
            // Clean up old entries (> 10 minutes)
            var expiredKeys = _stateCache
                .Where(kvp => kvp.Value.CreatedAt < DateTime.UtcNow.AddMinutes(-10))
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredKeys)
            {
                _stateCache.Remove(key);
            }
            
            // Also clean up old code mappings
            var expiredCodes = _codeToStateCache
                .Where(kvp => !_stateCache.ContainsKey(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredCodes)
            {
                _codeToStateCache.Remove(key);
            }
            
            // Store by state for later retrieval
            _stateCache[state ?? ""] = flowState;
            _logger.LogInformation("Stored OAuth flow state in cache for state: {State}", state);
        }
        
        // Extract prompt parameter from query string or use default
        var prompt = context.Request.Query["prompt"].FirstOrDefault() ?? "select_account";
        
        // Use our PKCE challenge with Microsoft
        var microsoftAuthUrl = $"{authority}/oauth2/v2.0/authorize" +
            $"?client_id={azureClientId}" +
            $"&response_type={responseType}" +
            $"&redirect_uri={Uri.EscapeDataString(registeredRedirectUri)}" +
            $"&scope={Uri.EscapeDataString(scope ?? "User.Read openid profile")}" +
            $"&state={state}" +
            $"&code_challenge={ourCodeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&prompt={prompt}";

        return Task.FromResult(Results.Redirect(microsoftAuthUrl));
    }

    /// <summary>
    /// OAuth 2.1 compliant redirect URI handling for loopback addresses.
    /// Implements OAuth 2.1 Section 8.4.2 - Loopback Interface Redirection.
    /// </summary>
    private string DetermineRegisteredRedirectUri(string clientRedirectUri, AuthenticationConfiguration config)
    {
        // Get the first registered redirect URI as our canonical one
        var registeredUris = config.ExternalIdP.AzureAD.RedirectUris;
        if (registeredUris == null || !registeredUris.Any())
        {
            throw new InvalidOperationException("No redirect URIs configured in Azure AD settings");
        }

        var canonicalUri = registeredUris.First();
        
        try
        {
            var clientUri = new Uri(clientRedirectUri);
            var registeredUri = new Uri(canonicalUri);
            
            // OAuth 2.1 Section 8.4.2: For loopback interfaces (localhost/127.0.0.1),
            // the authorization server MUST allow any port number.
            // Everything else must match exactly: scheme, host, path, query, fragment
            
            bool isLoopback = IsLoopbackAddress(clientUri.Host) && IsLoopbackAddress(registeredUri.Host);
            
            if (isLoopback)
            {
                // For loopback, verify everything except port matches
                bool schemeMatches = clientUri.Scheme.Equals(registeredUri.Scheme, StringComparison.OrdinalIgnoreCase);
                bool hostMatches = clientUri.Host.Equals(registeredUri.Host, StringComparison.OrdinalIgnoreCase);
                bool pathMatches = clientUri.AbsolutePath.Equals(registeredUri.AbsolutePath, StringComparison.Ordinal);
                bool queryMatches = clientUri.Query.Equals(registeredUri.Query, StringComparison.Ordinal);
                bool fragmentMatches = clientUri.Fragment.Equals(registeredUri.Fragment, StringComparison.Ordinal);
                
                if (schemeMatches && hostMatches && pathMatches && queryMatches && fragmentMatches)
                {
                    // OAuth 2.1 compliant: Accept loopback with different port
                    // But use registered URI for Azure AD to satisfy Microsoft's exact matching
                    return canonicalUri;
                }
            }
            else
            {
                // Non-loopback addresses require exact string matching per OAuth 2.1
                if (clientRedirectUri.Equals(canonicalUri, StringComparison.Ordinal))
                {
                    return canonicalUri;
                }
            }
        }
        catch (UriFormatException)
        {
            // Invalid URI format - reject
        }
        
        throw new UnauthorizedAccessException($"Redirect URI '{clientRedirectUri}' does not match registered URI '{canonicalUri}' per OAuth 2.1 requirements");
    }

    /// <summary>
    /// Determines if a host is a loopback address per OAuth 2.1 specification.
    /// </summary>
    private static bool IsLoopbackAddress(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("127.0.0.1", StringComparison.Ordinal) ||
               host.Equals("::1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Handles OAuth token exchange by validating client's PKCE and issuing our own JWT.
    /// </summary>
    public async Task<IResult> HandleTokenAsync(string grantType, string? code, string? redirectUri,
        string? clientId, string? clientSecret, string? codeVerifier, HttpContext context)
    {
        _logger.LogCritical("🔑 TOKEN ENDPOINT: grantType={GrantType}, code={Code}, clientId={ClientId}, redirectUri={RedirectUri}, hasCodeVerifier={HasCodeVerifier}",
            grantType, code?.Substring(0, Math.Min(20, code?.Length ?? 0)) + "...", clientId, redirectUri, !string.IsNullOrEmpty(codeVerifier));
            
        if (grantType != "authorization_code" || string.IsNullOrEmpty(code))
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        // Extract state from the authorization code using static cache
        string state;
        OAuthFlowState? flowState = null;
        
        lock (_cacheLock)
        {
            // Try to get state from code mapping
            if (_codeToStateCache.TryGetValue(code, out var mappedState))
            {
                state = mappedState;
                _stateCache.TryGetValue(state, out flowState);
                _logger.LogInformation("Retrieved state for code: {Code} -> {State}", 
                    code.Substring(0, Math.Min(10, code.Length)) + "...", state);
            }
            else
            {
                state = string.Empty;
                _logger.LogWarning("No state found for code: {Code}", 
                    code.Substring(0, Math.Min(10, code.Length)) + "...");
            }
        }
        
        // Retrieve stored client PKCE parameters from static cache
        if (flowState == null)
        {
            _logger.LogError("No flow state found for state: {State}", state);
            return Results.BadRequest(new { error = "invalid_request", error_description = "Invalid or expired authorization flow" });
        }
        
        var storedClientId = flowState.ClientId;
        var clientCodeChallenge = flowState.CodeChallenge;
        var clientChallengeMethod = flowState.CodeChallengeMethod;
        var clientRedirectUri = flowState.RedirectUri;
        var ourCodeVerifier = flowState.CodeVerifier;
        var microsoftAuthCode = flowState.MicrosoftAuthCode; // Get Microsoft's authorization code
        
        // Validate client credentials
        if (storedClientId != clientId)
        {
            _logger.LogError("Client ID mismatch: expected {Expected}, got {Actual}", storedClientId, clientId);
            return Results.BadRequest(new { error = "invalid_client" });
        }
        
        // Validate client's PKCE if provided
        if (!string.IsNullOrEmpty(clientCodeChallenge) && !string.IsNullOrEmpty(codeVerifier))
        {
            var computedChallenge = GeneratePKCECodeChallenge(codeVerifier, clientChallengeMethod);
            if (computedChallenge != clientCodeChallenge)
            {
                _logger.LogError("PKCE verification failed");
                return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed" });
            }
        }
        
        // Check if we have Microsoft's authorization code
        if (string.IsNullOrEmpty(microsoftAuthCode))
        {
            _logger.LogError("No Microsoft authorization code found for state: {State}", state);
            return Results.BadRequest(new { error = "invalid_grant", error_description = "Authorization code not found or expired" });
        }
        
        // Exchange Microsoft's code with Microsoft using our PKCE
        var config = _authConfig.CurrentValue;
        var authority = config.ExternalIdP.AzureAD.Authority;
        var azureClientId = config.ExternalIdP.AzureAD.ClientId;
        
        using var httpClient = new HttpClient();
        var tokenEndpoint = $"{authority}/oauth2/v2.0/token";
        var registeredRedirectUri = DetermineRegisteredRedirectUri(clientRedirectUri ?? redirectUri ?? "", config);
        
        var formData = new List<KeyValuePair<string, string>>
        {
            new("client_id", azureClientId),
            new("grant_type", "authorization_code"),
            new("code", microsoftAuthCode), // Use Microsoft's code, not our code
            new("redirect_uri", registeredRedirectUri),
            new("scope", "User.Read openid profile")
        };
        
        // Use our stored code verifier for Microsoft
        if (!string.IsNullOrEmpty(ourCodeVerifier))
        {
            formData.Add(new("code_verifier", ourCodeVerifier));
        }
        
        // Add client secret if configured
        var azureClientSecret = config.ExternalIdP.ClientSecret;
        if (!string.IsNullOrEmpty(azureClientSecret))
        {
            formData.Add(new("client_secret", azureClientSecret));
        }
        
        var formContent = new FormUrlEncodedContent(formData);
        
        try
        {
            var response = await httpClient.PostAsync(tokenEndpoint, formContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogCritical("🔑 MICROSOFT TOKEN RESPONSE: StatusCode={StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Microsoft token exchange failed: {ResponseContent}", responseContent);
                return Results.BadRequest(new { error = "token_exchange_failed", details = responseContent });
            }
            
            // Parse Microsoft's response to get user information
            var microsoftTokens = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
            var idToken = microsoftTokens?["id_token"]?.ToString();
            
            if (string.IsNullOrEmpty(idToken))
            {
                return Results.BadRequest(new { error = "invalid_response", error_description = "No ID token from Microsoft" });
            }
            
            // Parse the ID token to get user claims
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(idToken);
            
            // Create our own JWT token for the MCP client
            var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
            var claims = new List<System.Security.Claims.Claim>
            {
                new("sub", jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? ""),
                new("name", jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? ""),
                new("email", jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? 
                         jsonToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value ?? ""),
                new("client_id", clientId ?? "mcp-remote"),
                new("scope", "mcp:tools")
            };
            
            var principal = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(claims, "oauth2"));
            
            var scopes = new[] { "mcp:tools" };
            var accessToken = await tokenService.CreateAccessTokenAsync(principal, clientId ?? "mcp-remote", scopes);
            var refreshToken = await tokenService.CreateRefreshTokenAsync(principal, clientId ?? "mcp-remote", "oauth");
            
            // Clean up static cache data to prevent code reuse
            lock (_cacheLock)
            {
                _stateCache.Remove(state);
                _codeToStateCache.Remove(code);
                // Also clean up Microsoft's code to prevent reuse
                if (flowState != null)
                {
                    flowState.MicrosoftAuthCode = string.Empty;
                }
            }
            
            // Return our JWT tokens
            var tokenResponse = new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = 3600,
                refresh_token = refreshToken,
                scope = "mcp:tools"
            };
            
            return Results.Json(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange error");
            return Results.BadRequest(new { error = "token_exchange_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Handles user info endpoint.
    /// </summary>
    public Task<IResult> HandleUserInfoAsync(HttpContext context)
    {
        return Task.FromResult(Results.BadRequest(new { error = "userinfo_not_implemented" }));
    }

    /// <summary>
    /// Indicates this provider handles Azure AD authentication based on configuration.
    /// </summary>
    public bool CanHandle(string authenticationMode, string? providerName)
    {
        var config = _authConfig.CurrentValue;
        return authenticationMode == "AuthorizationServer" && 
               config.ExternalIdP.Provider == "AzureAD";
    }
    
    /// <summary>
    /// Generates a cryptographically secure PKCE code verifier.
    /// </summary>
    private static string GeneratePKCECodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
    
    /// <summary>
    /// Generates PKCE code challenge from verifier.
    /// </summary>
    private static string GeneratePKCECodeChallenge(string codeVerifier, string method = "S256")
    {
        if (method == "plain")
        {
            return codeVerifier;
        }
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var challengeBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
    
}

/// <summary>
/// Factory for creating OAuth endpoint providers.
/// </summary>
public class OAuthEndpointProviderFactory : IOAuthEndpointProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    private readonly ILogger<OAuthEndpointProviderFactory> _logger;

    public OAuthEndpointProviderFactory(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ILogger<OAuthEndpointProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _authConfig = authConfig;
        _logger = logger;
    }

    public IOAuthEndpointProvider GetProvider()
    {
        var config = _authConfig.CurrentValue;
        
        // Choose provider based on configuration
        if (config.ExternalIdP.Provider == "AzureAD")
        {
            _logger.LogInformation("Using Azure AD OAuth provider");
            return _serviceProvider.GetRequiredService<SimpleOAuthEndpointProvider>();
        }
        else
        {
            _logger.LogInformation("Using Local OAuth provider for mcp-remote compatibility");
            return _serviceProvider.GetRequiredService<LocalOAuthEndpointProvider>();
        }
    }
}