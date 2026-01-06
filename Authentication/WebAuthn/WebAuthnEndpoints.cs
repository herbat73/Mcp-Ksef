using Authentication.WebAuthn;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Authentication.WebAuthn;

/// <summary>
/// WebAuthn endpoint implementations for enterprise passwordless authentication.
/// </summary>
public static class WebAuthnEndpoints
{
    /// <summary>
    /// Maps WebAuthn endpoints for enterprise integration.
    /// </summary>
    public static void MapWebAuthnEndpoints(this WebApplication app)
    {
        // WebAuthn registration page
        app.MapGet("/webauthn/register", () => Results.Content(CreateRegistrationPage(), "text/html"));
        
        // WebAuthn registration endpoints
        app.MapPost("/webauthn/register/begin", BeginRegistration);
        app.MapPost("/webauthn/register/complete", CompleteRegistration);

        // WebAuthn authentication endpoints  
        app.MapPost("/webauthn/authenticate/begin", BeginAuthentication);
        app.MapPost("/webauthn/authenticate/complete", CompleteAuthentication);

        // Enterprise management endpoints
        app.MapGet("/webauthn/statistics/{tenantId}", GetStatistics);
        app.MapDelete("/webauthn/credentials/{credentialId}", RevokeCredential);
    }

    /// <summary>
    /// Begins WebAuthn registration process.
    /// </summary>
    private static async Task<IResult> BeginRegistration(
        HttpContext context,
        IEnterpriseWebAuthnService webAuthnService,
        ILogger<IEnterpriseWebAuthnService> logger)
    {
        try
        {
            var request = await context.Request.ReadFromJsonAsync<RegistrationRequest>();
            if (request == null)
            {
                return Results.BadRequest("Invalid request");
            }

            var options = await webAuthnService.BeginRegistrationAsync(
                request.UserId, request.DisplayName, request.TenantId);

            // Store challenge in session
            context.Session.SetString("webauthn_challenge", Convert.ToBase64String(options.Challenge));
            context.Session.SetString("webauthn_user", request.UserId);
            context.Session.SetString("webauthn_tenant", request.TenantId);

            return Results.Ok(options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error beginning WebAuthn registration");
            return Results.Problem("Registration error");
        }
    }

    /// <summary>
    /// Completes WebAuthn registration process.
    /// </summary>
    private static async Task<IResult> CompleteRegistration(
        HttpContext context,
        IEnterpriseWebAuthnService webAuthnService,
        ILogger<IEnterpriseWebAuthnService> logger)
    {
        try
        {
            var challenge = context.Session.GetString("webauthn_challenge");
            if (string.IsNullOrEmpty(challenge))
            {
                return Results.BadRequest("No active session");
            }

            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var result = await webAuthnService.CompleteRegistrationAsync(requestBody, challenge);

            // Clear session
            context.Session.Remove("webauthn_challenge");
            context.Session.Remove("webauthn_user");
            context.Session.Remove("webauthn_tenant");

            if (result.IsSuccessful)
            {
                return Results.Ok(new { status = "success", credential_id = result.CredentialId });
            }
            else
            {
                return Results.BadRequest(new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing WebAuthn registration");
            return Results.Problem("Registration completion error");
        }
    }

    /// <summary>
    /// Begins WebAuthn authentication process.
    /// </summary>
    private static async Task<IResult> BeginAuthentication(
        HttpContext context,
        IEnterpriseWebAuthnService webAuthnService,
        ILogger<IEnterpriseWebAuthnService> logger)
    {
        try
        {
            var request = await context.Request.ReadFromJsonAsync<AuthenticationRequest>();
            if (request == null)
            {
                return Results.BadRequest("Invalid request");
            }

            var options = await webAuthnService.BeginAuthenticationAsync(request.UserId, request.TenantId);

            // Store challenge in session
            context.Session.SetString("webauthn_auth_challenge", Convert.ToBase64String(options.Challenge));
            context.Session.SetString("webauthn_auth_user", request.UserId);
            context.Session.SetString("webauthn_auth_tenant", request.TenantId);

            return Results.Ok(options);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error beginning WebAuthn authentication for user {User}", 
                context.Session.GetString("webauthn_auth_user"));
            return Results.Problem("Authentication error");
        }
    }

    /// <summary>
    /// Completes WebAuthn authentication process.
    /// </summary>
    private static async Task<IResult> CompleteAuthentication(
        HttpContext context,
        IEnterpriseWebAuthnService webAuthnService,
        ILogger<IEnterpriseWebAuthnService> logger)
    {
        try
        {
            var challenge = context.Session.GetString("webauthn_auth_challenge");
            var userId = context.Session.GetString("webauthn_auth_user");
            var tenantId = context.Session.GetString("webauthn_auth_tenant");

            if (string.IsNullOrEmpty(challenge) || string.IsNullOrEmpty(userId))
            {
                return Results.BadRequest("No active session");
            }

            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var result = await webAuthnService.CompleteAuthenticationAsync(requestBody, challenge);

            // Clear session
            context.Session.Remove("webauthn_auth_challenge");
            context.Session.Remove("webauthn_auth_user");
            context.Session.Remove("webauthn_auth_tenant");

            if (result.IsAuthenticated)
            {
                return Results.Ok(new { 
                    status = "success", 
                    user_id = result.UserId,
                    tenant_id = result.TenantId 
                });
            }
            else
            {
                return Results.Unauthorized();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing WebAuthn authentication");
            return Results.Problem("Authentication completion error");
        }
    }

    /// <summary>
    /// Gets WebAuthn statistics for enterprise monitoring.
    /// </summary>
    private static async Task<IResult> GetStatistics(
        string tenantId,
        IEnterpriseWebAuthnService webAuthnService)
    {
        try
        {
            var stats = await webAuthnService.GetStatisticsAsync(tenantId);
            return Results.Ok(stats);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error retrieving statistics ex: {ex.Message}");
        }
    }

    /// <summary>
    /// Revokes WebAuthn credential.
    /// </summary>
    private static async Task<IResult> RevokeCredential(
        string credentialId,
        IEnterpriseWebAuthnService webAuthnService)
    {
        try
        {
            var success = await webAuthnService.RevokeCredentialAsync(credentialId);
            if (success)
            {
                return Results.Ok(new { status = "revoked" });
            }
            else
            {
                return Results.NotFound();
            }
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error revoking credential ex: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates HTML registration page for WebAuthn.
    /// </summary>
    private static string CreateRegistrationPage()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <title>WebAuthn Registration</title>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'>
    <link href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css' rel='stylesheet'>
    <style>
        body { background-color: #f8f9fa; }
        .main-container { max-width: 700px; margin: 50px auto; }
        .card { box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1); border: none; }
        .btn-register { background: linear-gradient(45deg, #28a745, #20c997); border: none; }
        .btn-register:hover { background: linear-gradient(45deg, #218838, #1ba085); }
        .step-indicator { border-left: 4px solid #007bff; }
        .icon-large { font-size: 1.5rem; }
    </style>
</head>
<body>
    <div class='main-container'>
        <div class='text-center mb-4'>
            <h1 class='display-5'><i class='bi bi-shield-lock icon-large'></i> WebAuthn Registration</h1>
            <p class='lead'>Secure biometric and security key authentication</p>
        </div>
        
        <div class='card mb-4'>
            <div class='card-body bg-primary text-white'>
                <h5 class='card-title'><i class='bi bi-info-circle'></i> Register your biometric or security key</h5>
                <p class='card-text mb-0'>Enable passwordless authentication using your fingerprint, face recognition, or hardware security key (YubiKey, etc.) for OAuth flows.</p>
            </div>
        </div>

        <div class='card mb-4'>
            <div class='card-body step-indicator'>
                <h5 class='card-title'><i class='bi bi-1-circle'></i> Step 1: Enter your details</h5>
                
                <div class='mb-3'>
                    <label for='userId' class='form-label'><i class='bi bi-person'></i> User ID (email):</label>
                    <input type='text' class='form-control' id='userId' value='test.user@company.com' placeholder='Enter your user ID' />
                </div>
                
                <div class='mb-3'>
                    <label for='displayName' class='form-label'><i class='bi bi-tag'></i> Display Name:</label>
                    <input type='text' class='form-control' id='displayName' value='Test User' placeholder='Enter display name' />
                </div>
                
                <button id='registerBtn' class='btn btn-register btn-lg w-100' onclick='registerCredential()'>
                    <i class='bi bi-shield-plus'></i> Register WebAuthn Credential
                </button>
            </div>
        </div>

        <div id='messages'></div>

        <div class='card'>
            <div class='card-body'>
                <h5 class='card-title'><i class='bi bi-list-check'></i> Instructions</h5>
                <ul class='list-unstyled'>
                    <li class='mb-2'><i class='bi bi-check-circle text-success'></i> Make sure your browser supports WebAuthn (Chrome, Firefox, Safari, Edge)</li>
                    <li class='mb-2'><i class='bi bi-fingerprint text-primary'></i> For biometrics: Ensure your device has fingerprint/face recognition enabled</li>
                    <li class='mb-2'><i class='bi bi-key text-warning'></i> For security keys: Have your YubiKey or other FIDO2 device ready</li>
                    <li class='mb-0'><i class='bi bi-shield-check text-info'></i> After registration, you can use this credential in OAuth authentication flows</li>
                </ul>
            </div>
        </div>
    </div>

    <script>
        async function registerCredential() {
            const messageDiv = document.getElementById('messages');
            const registerBtn = document.getElementById('registerBtn');
            
            messageDiv.innerHTML = '';
            registerBtn.disabled = true;
            registerBtn.innerHTML = '<i class=""bi bi-hourglass-split""></i> Registering...';
            
            try {
                if (!window.PublicKeyCredential) {
                    throw new Error('WebAuthn is not supported in this browser');
                }
                
                const userId = document.getElementById('userId').value;
                const displayName = document.getElementById('displayName').value;
                
                if (!userId || !displayName) {
                    throw new Error('Please enter both User ID and Display Name');
                }
                
                // Call WebAuthn registration endpoints
                const beginResponse = await fetch('/webauthn/register/begin', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        userId: userId,
                        displayName: displayName,
                        tenantId: 'default'
                    })
                });
                
                if (!beginResponse.ok) {
                    throw new Error('Failed to begin WebAuthn registration');
                }
                
                const credentialCreationOptions = await beginResponse.json();
                
                // Convert base64url to ArrayBuffer for WebAuthn API
                credentialCreationOptions.challenge = Uint8Array.from(
                    atob(credentialCreationOptions.challenge.replace(/-/g, '+').replace(/_/g, '/')), 
                    c => c.charCodeAt(0)
                );
                credentialCreationOptions.user.id = Uint8Array.from(
                    atob(credentialCreationOptions.user.id.replace(/-/g, '+').replace(/_/g, '/')), 
                    c => c.charCodeAt(0)
                );
                
                // Create credential using WebAuthn API
                const credential = await navigator.credentials.create({
                    publicKey: credentialCreationOptions
                });
                
                if (!credential) {
                    throw new Error('No credential created');
                }
                
                // Convert credential response to format server expects
                const credentialData = {
                    id: credential.id,
                    rawId: Array.from(new Uint8Array(credential.rawId)),
                    response: {
                        attestationObject: Array.from(new Uint8Array(credential.response.attestationObject)),
                        clientDataJSON: Array.from(new Uint8Array(credential.response.clientDataJSON))
                    },
                    type: credential.type
                };
                
                // Complete registration
                const completeResponse = await fetch('/webauthn/register/complete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        userId: userId,
                        tenantId: 'default',
                        credentialData: credentialData
                    })
                });
                
                if (!completeResponse.ok) {
                    const errorText = await completeResponse.text();
                    throw new Error(`Registration completion failed: ${errorText}`);
                }
                
                const result = await completeResponse.json();
                messageDiv.innerHTML = '<div class=""alert alert-success""><i class=""bi bi-check-circle""></i> WebAuthn credential registered successfully! You can now use this credential for OAuth authentication.</div>';
                
            } catch (error) {
                console.error('Registration error:', error);
                messageDiv.innerHTML = `<div class=""alert alert-danger""><i class=""bi bi-exclamation-triangle""></i> Registration failed: ${error.message}</div>`;
            } finally {
                registerBtn.disabled = false;
                registerBtn.innerHTML = '<i class=""bi bi-shield-plus""></i> Register WebAuthn Credential';
            }
        }
    </script>
</body>
</html>";
    }
}

/// <summary>
/// Request models for WebAuthn endpoints.
/// </summary>

public class RegistrationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
}

public class AuthenticationRequest
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
}