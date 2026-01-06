using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;

namespace Authentication.Services;

/// <summary>
/// Enterprise client certificate authentication service.
/// Implements mutual TLS (mTLS) authentication for enhanced security.
/// </summary>
public interface IClientCertificateService
{
    /// <summary>
    /// Validates client certificate against enterprise policies.
    /// </summary>
    Task<CertificateValidationResult> ValidateClientCertificateAsync(X509Certificate2 certificate, string tenantId);

    /// <summary>
    /// Extracts client identity from certificate.
    /// </summary>
    Task<ClaimsPrincipal?> GetClientIdentityFromCertificateAsync(X509Certificate2 certificate);

    /// <summary>
    /// Registers client certificate for enterprise management.
    /// </summary>
    Task<bool> RegisterClientCertificateAsync(string clientId, X509Certificate2 certificate, string tenantId);

    /// <summary>
    /// Revokes client certificate for security incidents.
    /// </summary>
    Task<bool> RevokeClientCertificateAsync(string certificateThumbprint, string reason);
}

/// <summary>
/// Enterprise client certificate service implementation.
/// </summary>
public class ClientCertificateService : IClientCertificateService
{
    private readonly ILogger<ClientCertificateService> _logger;
    
    // In-memory certificate store for development
    private readonly Dictionary<string, RegisteredCertificate> _registeredCertificates = new();
    private readonly HashSet<string> _revokedCertificates = new();

    public ClientCertificateService(ILogger<ClientCertificateService> logger)
    {
        _logger = logger;
        InitializeTestCertificates();
    }

    /// <summary>
    /// Validates client certificate with enterprise security policies.
    /// </summary>
    public async Task<CertificateValidationResult> ValidateClientCertificateAsync(
        X509Certificate2 certificate, string tenantId)
    {
        try
        {
            _logger.LogDebug("Validating client certificate {Thumbprint} for tenant {Tenant}",
                certificate.Thumbprint, tenantId);

            // Check if certificate is revoked
            if (_revokedCertificates.Contains(certificate.Thumbprint))
            {
                _logger.LogWarning("Revoked certificate {Thumbprint} attempted for tenant {Tenant}",
                    certificate.Thumbprint, tenantId);
                return CertificateValidationResult.Revoked("Certificate has been revoked");
            }

            // Check certificate validity period
            if (certificate.NotAfter < DateTime.UtcNow)
            {
                _logger.LogWarning("Expired certificate {Thumbprint} attempted for tenant {Tenant}",
                    certificate.Thumbprint, tenantId);
                return CertificateValidationResult.Expired("Certificate has expired");
            }

            if (certificate.NotBefore > DateTime.UtcNow)
            {
                _logger.LogWarning("Certificate {Thumbprint} not yet valid for tenant {Tenant}",
                    certificate.Thumbprint, tenantId);
                return CertificateValidationResult.Invalid("Certificate not yet valid");
            }

            // Check if certificate is registered for this tenant
            if (_registeredCertificates.TryGetValue(certificate.Thumbprint, out var registered))
            {
                if (registered.TenantId != tenantId)
                {
                    _logger.LogWarning("Certificate {Thumbprint} registered for different tenant {RegisteredTenant}, requested for {RequestedTenant}",
                        certificate.Thumbprint, registered.TenantId, tenantId);
                    return CertificateValidationResult.Invalid("Certificate not registered for this tenant");
                }

                return CertificateValidationResult.Valid("Certificate validation successful");
            }

            // For development, allow self-signed certificates
            if (IsDevelopmentEnvironment())
            {
                _logger.LogDebug("Development mode: accepting unregistered certificate {Thumbprint}",
                    certificate.Thumbprint);
                return CertificateValidationResult.Valid("Certificate accepted in development mode");
            }

            _logger.LogWarning("Unregistered certificate {Thumbprint} attempted for tenant {Tenant}",
                certificate.Thumbprint, tenantId);
            return CertificateValidationResult.Invalid("Certificate not registered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client certificate {Thumbprint}",
                certificate.Thumbprint);
            return CertificateValidationResult.Error("Certificate validation error");
        }
    }

    /// <summary>
    /// Extracts client identity claims from certificate.
    /// </summary>
    public async Task<ClaimsPrincipal?> GetClientIdentityFromCertificateAsync(X509Certificate2 certificate)
    {
        try
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, certificate.Subject),
                new(ClaimTypes.Thumbprint, certificate.Thumbprint),
                new("cert_issuer", certificate.Issuer),
                new("cert_serial", certificate.SerialNumber),
                new("auth_method", "client_certificate")
            };

            // Extract additional claims from certificate extensions
            foreach (var extension in certificate.Extensions)
            {
                if (extension is X509SubjectAlternativeNameExtension sanExtension)
                {
                    foreach (var dnsName in sanExtension.EnumerateDnsNames())
                    {
                        claims.Add(new Claim("cert_san_dns", dnsName));
                    }
                }
            }

            var identity = new ClaimsIdentity(claims, "client_certificate");
            var principal = new ClaimsPrincipal(identity);

            _logger.LogDebug("Extracted client identity from certificate {Thumbprint}: {Subject}",
                certificate.Thumbprint, certificate.Subject);

            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting client identity from certificate {Thumbprint}",
                certificate.Thumbprint);
            return null;
        }
    }

    /// <summary>
    /// Registers client certificate for enterprise management.
    /// </summary>
    public async Task<bool> RegisterClientCertificateAsync(string clientId, X509Certificate2 certificate, string tenantId)
    {
        try
        {
            var registered = new RegisteredCertificate
            {
                ClientId = clientId,
                Thumbprint = certificate.Thumbprint,
                TenantId = tenantId,
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                ExpiresAt = certificate.NotAfter,
                RegisteredAt = DateTime.UtcNow,
                IsActive = true
            };

            _registeredCertificates[certificate.Thumbprint] = registered;

            _logger.LogInformation("Registered client certificate {Thumbprint} for client {Client} in tenant {Tenant}",
                certificate.Thumbprint, clientId, tenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering client certificate {Thumbprint}",
                certificate.Thumbprint);
            return false;
        }
    }

    /// <summary>
    /// Revokes client certificate for security incidents.
    /// </summary>
    public async Task<bool> RevokeClientCertificateAsync(string certificateThumbprint, string reason)
    {
        try
        {
            _revokedCertificates.Add(certificateThumbprint);

            if (_registeredCertificates.TryGetValue(certificateThumbprint, out var cert))
            {
                cert.IsActive = false;
                cert.RevokedAt = DateTime.UtcNow;
                cert.RevocationReason = reason;
            }

            _logger.LogWarning("Revoked client certificate {Thumbprint}, reason: {Reason}",
                certificateThumbprint, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking client certificate {Thumbprint}",
                certificateThumbprint);
            return false;
        }
    }

    /// <summary>
    /// Checks if running in development environment.
    /// </summary>
    private bool IsDevelopmentEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    }

    /// <summary>
    /// Initializes test certificates for development.
    /// </summary>
    private void InitializeTestCertificates()
    {
        // Development test certificate entries
        _logger.LogInformation("Initialized client certificate service for enterprise mTLS authentication");
    }
}

/// <summary>
/// Certificate validation result for enterprise security decisions.
/// </summary>
public class CertificateValidationResult
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
    public CertificateStatus Status { get; set; }

    public static CertificateValidationResult Valid(string reason) =>
        new() { IsValid = true, Reason = reason, Status = CertificateStatus.Valid };

    public static CertificateValidationResult Invalid(string reason) =>
        new() { IsValid = false, Reason = reason, Status = CertificateStatus.Invalid };

    public static CertificateValidationResult Expired(string reason) =>
        new() { IsValid = false, Reason = reason, Status = CertificateStatus.Expired };

    public static CertificateValidationResult Revoked(string reason) =>
        new() { IsValid = false, Reason = reason, Status = CertificateStatus.Revoked };

    public static CertificateValidationResult Error(string reason) =>
        new() { IsValid = false, Reason = reason, Status = CertificateStatus.Error };
}

/// <summary>
/// Registered certificate information for enterprise management.
/// </summary>
public class RegisteredCertificate
{
    public string ClientId { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Certificate status for enterprise security tracking.
/// </summary>
public enum CertificateStatus
{
    Valid,
    Invalid,
    Expired,
    Revoked,
    NotRegistered,
    Error
}