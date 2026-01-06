using Authentication.Interfaces;
using Authentication.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Authentication.Middleware;

/// <summary>
/// Enterprise rate limiting middleware - Sprint 1 implementation.
/// Focuses on core functionality with minimal dependencies.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IRateLimitingService _rateLimitingService;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IRateLimitingService rateLimitingService)
    {
        _next = next;
        _logger = logger;
        _rateLimitingService = rateLimitingService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Create simple auth request from HTTP context
            var authRequest = new AuthenticationRequest
            {
                ClientIPAddress = GetClientIP(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                RequestedTool = context.Request.Path.Value?.ToLowerInvariant() ?? "unknown"
            };

            // Check rate limiting
            var isAllowed = await _rateLimitingService.IsRequestAllowedAsync(authRequest);
            
            if (!isAllowed)
            {
                await HandleRateLimitExceeded(context, authRequest.ClientIPAddress);
                return;
            }

            // Continue pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate limiting middleware error - allowing request");
            await _next(context); // Fail open
        }
    }

    private string GetClientIP(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task HandleRateLimitExceeded(HttpContext context, string ip)
    {
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/json";
        
        var response = new { error = "rate_limit_exceeded", message = "Too many requests" };
        var json = JsonSerializer.Serialize(response);
        
        _logger.LogWarning("Rate limit exceeded for IP: {IP}", ip);
        await context.Response.WriteAsync(json);
    }
}