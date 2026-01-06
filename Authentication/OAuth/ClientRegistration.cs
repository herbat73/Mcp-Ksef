using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Authentication.OAuth;

/// <summary>
/// Dynamic Client Registration (DCR) implementation for mcp-remote compatibility.
/// Implements RFC7591 for automatic client registration.
/// </summary>
public static class ClientRegistration
{
    /// <summary>
    /// Maps OAuth client registration endpoint.
    /// </summary>
    public static void MapClientRegistrationEndpoint(this WebApplication app)
    {
        app.MapPost("/register", HandleClientRegistration);
    }

    /// <summary>
    /// Handles dynamic client registration requests from mcp-remote.
    /// </summary>
    private static async Task<IResult> HandleClientRegistration(
        HttpContext context,
        ILogger<IResult> logger)
    {
        try
        {
            logger.LogInformation("Processing dynamic client registration request");

            // Read registration request
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var registrationRequest = JsonSerializer.Deserialize<JsonElement>(requestBody);

            if (!registrationRequest.TryGetProperty("client_name", out var clientNameElement))
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = "client_name is required" });
            }

            var clientName = clientNameElement.GetString() ?? "unknown-client";
            
            // Extract redirect URIs
            var redirectUris = Array.Empty<string>();
            if (registrationRequest.TryGetProperty("redirect_uris", out var redirectElement))
            {
                redirectUris = redirectElement.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray()!;
            }

            // Validate redirect URIs for mcp-remote and Claude compatibility
            foreach (var uri in redirectUris)
            {
                if (!IsValidRedirectUri(uri))
                {
                    logger.LogWarning("Invalid redirect URI in client registration: {Uri}", uri);
                    return Results.BadRequest(new 
                    { 
                        error = "invalid_redirect_uri", 
                        error_description = $"Redirect URI not allowed: {uri}" 
                    });
                }
            }

            // Generate client credentials
            var clientId = GenerateClientId();
            var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Store client registration (in-memory for development)
            var client = new RegisteredClient
            {
                ClientId = clientId,
                ClientName = clientName,
                RedirectUris = redirectUris,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            StoreRegisteredClient(client);

            // Return DCR response per RFC7591
            var response = new
            {
                client_id = clientId,
                client_name = clientName,
                client_id_issued_at = issuedAt,
                redirect_uris = redirectUris,
                grant_types = new[] { "authorization_code" },
                response_types = new[] { "code" },
                token_endpoint_auth_method = "none"
            };

            logger.LogInformation("Dynamic client registration successful for {ClientName} with ID {ClientId}",
                clientName, clientId);

            return Results.Json(response, statusCode: 201);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing client registration");
            return Results.Problem("Client registration error");
        }
    }

    /// <summary>
    /// Validates redirect URI for enterprise security and mcp-remote compatibility.
    /// </summary>
    private static bool IsValidRedirectUri(string uri)
    {
        try
        {
            var parsedUri = new Uri(uri);
            
            // Allow localhost for mcp-remote (default callback)
            if (parsedUri.Host == "localhost" || parsedUri.Host == "127.0.0.1")
            {
                return true;
            }

            // Allow Claude.ai callback URL
            if (uri == "https://claude.ai/api/mcp/auth_callback" || 
                uri == "https://claude.com/api/mcp/auth_callback")
            {
                return true;
            }

            // For development, allow additional localhost variations
            if (parsedUri.Host.EndsWith(".localhost") || parsedUri.Host == "0.0.0.0")
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates secure client ID for OAuth registration.
    /// </summary>
    private static string GenerateClientId()
    {
        // Generate URL-safe client ID
        var bytes = new byte[16];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    // In-memory client storage for development
    private static readonly Dictionary<string, RegisteredClient> _registeredClients = new();

    private static void StoreRegisteredClient(RegisteredClient client)
    {
        _registeredClients[client.ClientId] = client;
    }

    public static RegisteredClient? GetRegisteredClient(string clientId)
    {
        return _registeredClients.TryGetValue(clientId, out var client) ? client : null;
    }
}

/// <summary>
/// Registered OAuth client for enterprise management.
/// </summary>
public class RegisteredClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string[] RedirectUris { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}