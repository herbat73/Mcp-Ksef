using System.Security.Claims;
using Authentication.Models;

namespace Authentication.Interfaces;

/// <summary>
/// Defines the contract for authentication mode providers in the enterprise MCP server.
/// Supports adaptive authentication patterns based on enterprise security requirements.
/// </summary>
public interface IAuthenticationModeProvider
{
    /// <summary>
    /// Gets the current authentication mode configuration.
    /// </summary>
    AuthenticationMode CurrentMode { get; }

    /// <summary>
    /// Validates an authentication request according to the current mode.
    /// </summary>
    /// <param name="request">The authentication request to validate</param>
    /// <returns>Authentication result with user claims if successful</returns>
    Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request);

    /// <summary>
    /// Determines if a specific authentication feature is supported in current mode.
    /// </summary>
    /// <param name="feature">The authentication feature to check</param>
    /// <returns>True if feature is supported in current mode</returns>
    Task<bool> SupportsFeatureAsync(AuthenticationFeature feature);

    /// <summary>
    /// Evaluates enterprise security policy for authentication decision.
    /// </summary>
    /// <param name="policy">The security policy to evaluate</param>
    /// <param name="context">The authentication context</param>
    /// <returns>Policy evaluation result</returns>
    Task<PolicyResult> EvaluatePolicyAsync(SecurityPolicy policy, AuthenticationContext context);

    /// <summary>
    /// Switches authentication mode at runtime without service interruption.
    /// </summary>
    /// <param name="newMode">The new authentication mode to activate</param>
    /// <returns>True if mode switch was successful</returns>
    Task<bool> SwitchModeAsync(AuthenticationMode newMode);
}

