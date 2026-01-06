using Microsoft.EntityFrameworkCore;
using Data.Entities;

namespace Data;

/// <summary>
/// Entity Framework database context for enterprise authentication data.
/// Follows .NET best practices for enterprise data access patterns.
/// </summary>
public class AuthDbContext : DbContext
{
    /// <summary>
    /// OAuth client registrations for enterprise client management.
    /// </summary>
    public DbSet<OAuthClient> OAuthClients { get; set; } = null!;

    /// <summary>
    /// Authorization codes for OAuth PKCE flow security.
    /// </summary>
    public DbSet<AuthorizationCode> AuthorizationCodes { get; set; } = null!;

    /// <summary>
    /// WebAuthn credentials for enterprise passwordless authentication.
    /// </summary>
    public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; } = null!;

    /// <summary>
    /// Enterprise user entities for authentication and authorization.
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Token revocations for enterprise token lifecycle management.
    /// </summary>
    public DbSet<TokenRevocation> TokenRevocations { get; set; } = null!;

    /// <summary>
    /// Comprehensive audit logs for enterprise compliance and security monitoring.
    /// </summary>
    public DbSet<AuditLogEntry> AuditLogs { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the AuthDbContext.
    /// </summary>
    /// <param name="options">Database context options for configuration</param>
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Configures entity relationships and enterprise data constraints.
    /// </summary>
    /// <param name="modelBuilder">Entity framework model builder</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // OAuth Client configuration
        modelBuilder.Entity<OAuthClient>(entity =>
        {
            entity.HasKey(e => e.ClientId);
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_oauth_clients_tenant_id");
            entity.HasIndex(e => e.ApprovalStatus).HasDatabaseName("IX_oauth_clients_approval_status");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_oauth_clients_created_at");
            
            // RedirectUris stored as semicolon-separated string
        });

        // Authorization Code configuration  
        modelBuilder.Entity<AuthorizationCode>(entity =>
        {
            entity.HasKey(e => e.Code);
            entity.HasIndex(e => e.ClientId).HasDatabaseName("IX_auth_codes_client_id");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_auth_codes_user_id");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("IX_auth_codes_expires_at");
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_auth_codes_tenant_id");

            entity.HasOne(e => e.Client)
                  .WithMany()
                  .HasForeignKey(e => e.ClientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // WebAuthn Credential configuration
        modelBuilder.Entity<WebAuthnCredential>(entity =>
        {
            entity.HasKey(e => e.CredentialId);
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_webauthn_creds_user_id");
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_webauthn_creds_tenant_id");
            entity.HasIndex(e => e.AuthenticatorAAGUID).HasDatabaseName("IX_webauthn_creds_aaguid");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_webauthn_creds_active");
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.UserPrincipalName).HasDatabaseName("IX_users_upn");
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_users_tenant_id");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_users_active");

            entity.HasMany(e => e.WebAuthnCredentials)
                  .WithOne()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Roles and ToolPermissions stored as semicolon-separated strings
        });

        // Token Revocation configuration
        modelBuilder.Entity<TokenRevocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenId).HasDatabaseName("IX_token_revocations_token_id");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_token_revocations_user_id");
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_token_revocations_tenant_id");
            entity.HasIndex(e => e.RevokedAt).HasDatabaseName("IX_token_revocations_revoked_at");
            entity.HasIndex(e => e.TokenExpiresAt).HasDatabaseName("IX_token_revocations_expires_at");
        });

        // Audit Log configuration
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventType).HasDatabaseName("IX_audit_logs_event_type");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_audit_logs_user_id");
            entity.HasIndex(e => e.TenantId).HasDatabaseName("IX_audit_logs_tenant_id");
            entity.HasIndex(e => e.Timestamp).HasDatabaseName("IX_audit_logs_timestamp");
            entity.HasIndex(e => e.Outcome).HasDatabaseName("IX_audit_logs_outcome");
            entity.HasIndex(e => e.RiskLevel).HasDatabaseName("IX_audit_logs_risk_level");

            // Enterprise audit log table configuration
        });

        // Enterprise data seeding for development
        SeedEnterpriseTestData(modelBuilder);
    }

    /// <summary>
    /// Seeds test data for enterprise development scenarios.
    /// </summary>
    /// <param name="modelBuilder">Entity framework model builder</param>
    private static void SeedEnterpriseTestData(ModelBuilder modelBuilder)
    {
        // Test users for development
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = "test-user-001",
                UserPrincipalName = "test.user@company.com",
                DisplayName = "Test User",
                Roles = "MCP_User",
                ToolPermissions = "Math;Utility;Data",
                TenantId = "default-tenant",
                IsActive = true
            },
            new User
            {
                UserId = "admin-user-001", 
                UserPrincipalName = "admin.user@company.com",
                DisplayName = "Admin User",
                Roles = "MCP_Admin;MCP_User",
                ToolPermissions = "Math;Utility;Data;Reflection",
                TenantId = "default-tenant",
                IsActive = true
            }
        );

        // Test OAuth client for development
        modelBuilder.Entity<OAuthClient>().HasData(
            new OAuthClient
            {
                ClientId = "test-mcp-client",
                ClientName = "Test MCP Client",
                ClientType = "public",
                RedirectUris = "http://localhost:3334/callback",
                GrantTypes = "authorization_code",
                Scopes = "mcp:tools",
                ApprovalStatus = ClientApprovalStatus.Approved,
                ApprovedBy = "system",
                TenantId = "default-tenant",
                ApprovedAt = DateTime.UtcNow,
                IsActive = true
            }
        );
    }

    /// <summary>
    /// Applies enterprise optimizations for production performance.
    /// </summary>
    /// <param name="optionsBuilder">Database context options builder</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enterprise performance optimizations
        optionsBuilder.EnableSensitiveDataLogging(false); // Security: no sensitive data in logs
        optionsBuilder.EnableDetailedErrors(false); // Performance: minimal error details in production
        optionsBuilder.EnableServiceProviderCaching(true); // Performance: cache service providers
        // Security: model validation enabled by default
    }

    /// <summary>
    /// Enterprise-grade change tracking for audit requirements.
    /// </summary>
    /// <returns>Number of entities saved</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Add audit trail for entity changes
        await AddAuditTrailAsync();
        
        // Apply enterprise timestamp patterns
        ApplyEnterpriseTimestamps();
        
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Adds comprehensive audit trail for enterprise compliance.
    /// </summary>
    private async Task AddAuditTrailAsync()
    {
        var auditEntries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || 
                       e.State == EntityState.Modified || 
                       e.State == EntityState.Deleted)
            .Select(entry => new AuditLogEntry
            {
                EventType = $"entity_{entry.State.ToString().ToLower()}",
                Description = $"{entry.Entity.GetType().Name} {entry.State}",
                Timestamp = DateTime.UtcNow,
                Outcome = "success",
                RiskLevel = "low",
                ComplianceFrameworks = "enterprise_data_governance"
            });

        await AuditLogs.AddRangeAsync(auditEntries);
    }

    /// <summary>
    /// Applies consistent timestamp patterns for enterprise data governance.
    /// </summary>
    private void ApplyEnterpriseTimestamps()
    {
        var timestamp = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Property("CreatedAt").CurrentValue == null)
                {
                    entry.Property("CreatedAt").CurrentValue = timestamp;
                }
            }
        }
    }
}