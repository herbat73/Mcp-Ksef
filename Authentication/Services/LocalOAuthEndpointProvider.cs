using Authentication.Configuration;
using Authentication.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Authentication.Services;

/// <summary>
/// Local OAuth endpoint provider for mcp-remote compatibility.
/// Handles OAuth 2.1 flow locally without external provider redirects.
/// </summary>
public class LocalOAuthEndpointProvider : IOAuthEndpointProvider
{
    private readonly IOptionsMonitor<AuthenticationConfiguration> _authConfig;
    private readonly ILogger<LocalOAuthEndpointProvider> _logger;

    public LocalOAuthEndpointProvider(
        IOptionsMonitor<AuthenticationConfiguration> authConfig,
        ILogger<LocalOAuthEndpointProvider> logger)
    {
        _authConfig = authConfig;
        _logger = logger;
    }

    /// <summary>
    /// Handles OAuth authorization locally for mcp-remote compatibility.
    /// Stores client_id in session and immediately redirects to callback with authorization code.
    /// </summary>
    public async Task<IResult> HandleAuthorizationAsync(
        string responseType, string clientId, string redirectUri,
        string? scope, string? state, string? codeChallenge, string? codeChallengeMethod,
        HttpContext context)
    {
        _logger.LogCritical("🔑 LOCAL AUTHORIZATION ENDPOINT HIT: ClientId={ClientId}, State={State}, RedirectUri={RedirectUri}", 
            clientId, state, redirectUri);

        try
        {
            // Validate OAuth 2.1 parameters
            if (responseType != "code")
            {
                return Results.BadRequest(new { error = "unsupported_response_type", error_description = "Only 'code' response type is supported" });
            }

            if (string.IsNullOrEmpty(clientId))
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = "client_id is required" });
            }

            if (string.IsNullOrEmpty(state))
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = "state parameter is required for security" });
            }

            // Store client_id and other OAuth parameters in session for callback
            context.Session.SetString($"client_id_{state}", clientId);
            context.Session.SetString($"redirect_uri_{state}", redirectUri);
            context.Session.SetString($"scope_{state}", scope ?? "mcp:tools");
            context.Session.SetString($"code_challenge_{state}", codeChallenge ?? "");
            context.Session.SetString($"code_challenge_method_{state}", codeChallengeMethod ?? "");
            
            await context.Session.CommitAsync(); // Ensure session data is persisted

            _logger.LogCritical("🔑 STORING LOCAL OAUTH PARAMS: ClientId={ClientId}, State={State}", clientId, state);

            // For mcp-remote compatibility, simulate immediate user consent and generate authorization code
            // In a real implementation, this would show a consent screen
            var authorizationCode = GenerateAuthorizationCode();
            
            // Store authorization code with state for token exchange
            context.Session.SetString($"auth_code_{state}", authorizationCode);
            context.Session.SetString($"auth_code_expires_{state}", DateTimeOffset.UtcNow.AddMinutes(10).ToString());
            await context.Session.CommitAsync();

            _logger.LogInformation("Generated authorization code for local OAuth flow: {CodePreview}...", authorizationCode.Substring(0, 8));

            // Redirect back to client with authorization code and state
            var callbackUrl = $"{redirectUri}?code={authorizationCode}&state={state}";
            _logger.LogInformation("Redirecting to callback URL: {CallbackUrl}", callbackUrl);
            
            return Results.Redirect(callbackUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in local OAuth authorization for client {ClientId}", clientId);
            return Results.BadRequest(new { error = "server_error", error_description = "Internal server error during authorization" });
        }
    }

    /// <summary>
    /// Handles OAuth token exchange for local OAuth flow.
    /// </summary>
    public async Task<IResult> HandleTokenAsync(string grantType, string? code, string? redirectUri,
        string? clientId, string? clientSecret, string? codeVerifier, HttpContext context)
    {
        _logger.LogCritical("🔑 LOCAL TOKEN ENDPOINT HIT: GrantType={GrantType}, Code={CodePreview}...", 
            grantType, code?.Length >= 8 ? code.Substring(0, 8) : code ?? "null");

        try
        {
            if (grantType != "authorization_code")
            {
                return Results.BadRequest(new { error = "unsupported_grant_type", error_description = "Only 'authorization_code' grant type is supported" });
            }

            if (string.IsNullOrEmpty(code))
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = "authorization code is required" });
            }

            // Find the state associated with this authorization code
            var sessionKeys = context.Session.Keys.ToList();
            string? matchingState = null;
            
            foreach (var key in sessionKeys)
            {
                if (key.StartsWith("auth_code_") && context.Session.GetString(key) == code)
                {
                    matchingState = key.Replace("auth_code_", "");
                    break;
                }
            }

            if (string.IsNullOrEmpty(matchingState))
            {
                _logger.LogWarning("Authorization code not found in session: {Code}", code);
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Authorization code is invalid or expired" });
            }

            // Verify code hasn't expired
            var expiryString = context.Session.GetString($"auth_code_expires_{matchingState}");
            if (DateTimeOffset.TryParse(expiryString, out var expiry) && expiry < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Authorization code expired for state: {State}", matchingState);
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Authorization code has expired" });
            }

            // Retrieve stored OAuth parameters
            var storedClientId = context.Session.GetString($"client_id_{matchingState}");
            var storedRedirectUri = context.Session.GetString($"redirect_uri_{matchingState}");
            var storedScope = context.Session.GetString($"scope_{matchingState}");
            var storedCodeChallenge = context.Session.GetString($"code_challenge_{matchingState}");

            _logger.LogCritical("🔑 RETRIEVED STORED PARAMS: ClientId={StoredClientId}, RequestClientId={RequestClientId}", 
                storedClientId, clientId);

            // Validate client_id matches (OAuth 2.1 requirement)
            if (storedClientId != clientId)
            {
                _logger.LogWarning("Client ID mismatch: stored={StoredClientId}, request={RequestClientId}", storedClientId, clientId);
                return Results.BadRequest(new { error = "invalid_client", error_description = "Client ID does not match authorization request" });
            }

            // Validate redirect_uri matches (OAuth 2.1 requirement)
            if (storedRedirectUri != redirectUri)
            {
                _logger.LogWarning("Redirect URI mismatch: stored={StoredRedirectUri}, request={RequestRedirectUri}", storedRedirectUri, redirectUri);
                return Results.BadRequest(new { error = "invalid_grant", error_description = "Redirect URI does not match authorization request" });
            }

            // Validate PKCE if present
            if (!string.IsNullOrEmpty(storedCodeChallenge) && !string.IsNullOrEmpty(codeVerifier))
            {
                if (!ValidateCodeVerifier(codeVerifier, storedCodeChallenge))
                {
                    _logger.LogWarning("PKCE validation failed for client {ClientId}", clientId);
                    return Results.BadRequest(new { error = "invalid_grant", error_description = "Code verifier validation failed" });
                }
            }

            // Generate tokens using the existing token service
            var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
            
            // Create a synthetic user for local OAuth (similar to callback behavior)
            var scopes = (storedScope ?? "mcp:tools").Split(' ');
            var userPrincipal = System.Security.Claims.ClaimsPrincipal.Current ?? new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("sub", "local-oauth-user"),
                    new System.Security.Claims.Claim("client_id", clientId),
                    new System.Security.Claims.Claim("scope", storedScope ?? "mcp:tools")
                }, "local-oauth"));

            var accessToken = await tokenService.CreateAccessTokenAsync(userPrincipal, clientId, scopes);
            var refreshToken = await tokenService.CreateRefreshTokenAsync(userPrincipal, clientId, "default");

            // Clean up session data
            context.Session.Remove($"client_id_{matchingState}");
            context.Session.Remove($"redirect_uri_{matchingState}");
            context.Session.Remove($"scope_{matchingState}");
            context.Session.Remove($"code_challenge_{matchingState}");
            context.Session.Remove($"code_challenge_method_{matchingState}");
            context.Session.Remove($"auth_code_{matchingState}");
            context.Session.Remove($"auth_code_expires_{matchingState}");

            _logger.LogInformation("Local OAuth token exchange successful for client {ClientId}", clientId);

            // Return OAuth 2.1 compliant token response
            var tokenResponse = new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = 28800, // 8 hours
                refresh_token = refreshToken,
                scope = storedScope ?? "mcp:tools"
            };

            return Results.Json(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in local OAuth token exchange");
            return Results.BadRequest(new { error = "server_error", error_description = "Internal server error during token exchange" });
        }
    }

    /// <summary>
    /// Handles user info endpoint (not implemented for local OAuth).
    /// </summary>
    public Task<IResult> HandleUserInfoAsync(HttpContext context)
    {
        return Task.FromResult(Results.BadRequest(new { error = "not_implemented", error_description = "User info endpoint not implemented for local OAuth" }));
    }

    /// <summary>
    /// Indicates this provider handles local OAuth authentication.
    /// </summary>
    public bool CanHandle(string authenticationMode, string? providerName)
    {
        // Handle local OAuth when not using external providers
        return authenticationMode == "AuthorizationServer" && 
               (providerName == "Local" || string.IsNullOrEmpty(providerName));
    }

    /// <summary>
    /// Generates a secure authorization code for OAuth flow.
    /// </summary>
    private string GenerateAuthorizationCode()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').Replace("=", "");
    }

    /// <summary>
    /// Validates PKCE code verifier against code challenge.
    /// </summary>
    private bool ValidateCodeVerifier(string codeVerifier, string codeChallenge)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(codeVerifier));
            var computedChallenge = Convert.ToBase64String(hashedBytes)
                .Replace('+', '-').Replace('/', '_').Replace("=", "");
            
            return computedChallenge == codeChallenge;
        }
        catch
        {
            return false;
        }
    }
}