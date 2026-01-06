using Authentication.Configuration;
using Authentication.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Authentication.OAuth;

/// <summary>
/// Refresh token grant handler for mcp-remote compatibility.
/// </summary>
public static class RefreshTokenHandler
{
    /// <summary>
    /// Handles refresh token grant requests from mcp-remote.
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

            // Issue new refresh token
            var newRefreshToken = await tokenService.CreateRefreshTokenAsync(
                principal, clientId, "default");

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
}