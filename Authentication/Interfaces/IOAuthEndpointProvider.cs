using Microsoft.AspNetCore.Mvc;

namespace Authentication.Interfaces;

/// <summary>
/// Abstraction for OAuth endpoint implementation following SOLID principles.
/// Allows different providers (local server, Azure AD, etc.) to handle OAuth flows.
/// </summary>
public interface IOAuthEndpointProvider
{
    /// <summary>
    /// Handles the OAuth authorization endpoint (/authorize).
    /// </summary>
    Task<IResult> HandleAuthorizationAsync(
        string responseType,
        string clientId,
        string redirectUri,
        string? scope,
        string? state,
        string? codeChallenge,
        string? codeChallengeMethod,
        HttpContext context);

    /// <summary>
    /// Handles the OAuth token endpoint (/token).
    /// </summary>
    Task<IResult> HandleTokenAsync(
        string grantType,
        string? code,
        string? redirectUri,
        string? clientId,
        string? clientSecret,
        string? codeVerifier,
        HttpContext context);

    /// <summary>
    /// Handles user info endpoint (/userinfo).
    /// </summary>
    Task<IResult> HandleUserInfoAsync(HttpContext context);

    /// <summary>
    /// Indicates if this provider can handle the current authentication mode.
    /// </summary>
    bool CanHandle(string authenticationMode, string? providerName);
}