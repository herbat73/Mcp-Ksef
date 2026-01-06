namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Authentication token value object following Microsoft DDD patterns.
/// Immutable token representation within User aggregate.
/// </summary>
public class AuthenticationToken : IEquatable<AuthenticationToken>
{
    public string TokenId { get; }
    public string UserId { get; }
    public string TenantId { get; }
    public string ClientId { get; }
    public DateTime IssuedAt { get; }
    public DateTime ExpiresAt { get; }
    public bool IsRevoked { get; private set; }
    public TokenType Type { get; }
    public string[] Scopes { get; }

    private AuthenticationToken(string tokenId, string userId, string tenantId, string clientId, 
        DateTime expiresAt, TokenType type, string[] scopes)
    {
        TokenId = tokenId;
        UserId = userId;
        TenantId = tenantId;
        ClientId = clientId;
        IssuedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
        Type = type;
        Scopes = scopes;
        IsRevoked = false;
    }

    public static AuthenticationToken Create(string userId, string tenantId, string clientId, 
        TimeSpan lifetime, TokenType type, string[] scopes)
    {
        string tokenId = Guid.NewGuid().ToString();
        DateTime expiresAt = DateTime.UtcNow.Add(lifetime);
        
        return new AuthenticationToken(tokenId, userId, tenantId, clientId, expiresAt, type, scopes);
    }

    public void Revoke()
    {
        if (IsRevoked)
            throw new InvalidOperationException($"Token {TokenId} is already revoked");
            
        IsRevoked = true;
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;

    public bool IsValid() => !IsRevoked && !IsExpired();

    public bool Equals(AuthenticationToken? other)
    {
        return other != null && TokenId == other.TokenId;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AuthenticationToken);
    }

    public override int GetHashCode()
    {
        return TokenId.GetHashCode();
    }
}

public enum TokenType
{
    AccessToken,
    RefreshToken
}