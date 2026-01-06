using Authentication.Domain.ValueObjects;

namespace Authentication.Domain.Entities;

/// <summary>
/// User aggregate root following Microsoft DDD patterns.
/// Represents the consistency boundary for user authentication.
/// </summary>
public class UserAggregate
{
    public UserId Id { get; private set; }
    public string TenantId { get; private set; }
    public List<AuthenticationToken> Tokens { get; private set; } = new();
    public AuthenticationState State { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastAuthenticatedAt { get; private set; }

    private UserAggregate(UserId id, string tenantId)
    {
        Id = id;
        TenantId = tenantId;
        State = AuthenticationState.Active;
        CreatedAt = DateTime.UtcNow;
        LastAuthenticatedAt = DateTime.UtcNow;
    }

    public static UserAggregate Create(string userId, string tenantId)
    {
        var id = UserId.Create(userId);
        return new UserAggregate(id, tenantId);
    }

    public AuthenticationToken IssueToken(string clientId, string[] scopes, TimeSpan lifetime, TokenType type)
    {
        if (State != AuthenticationState.Active)
            throw new InvalidOperationException($"Cannot issue token for inactive user {Id.Value}");

        var token = AuthenticationToken.Create(Id.Value, TenantId, clientId, lifetime, type, scopes);
        Tokens.Add(token);
        LastAuthenticatedAt = DateTime.UtcNow;
        
        return token;
    }

    public void RevokeToken(string tokenId)
    {
        var token = Tokens.FirstOrDefault(t => t.TokenId == tokenId);
        if (token == null)
            throw new ArgumentException($"Token {tokenId} not found for user {Id.Value}");

        token.Revoke();
    }

    public void DeactivateUser()
    {
        State = AuthenticationState.Deactivated;
        foreach (var token in Tokens.Where(t => !t.IsRevoked))
        {
            token.Revoke();
        }
    }

    public bool HasValidTokens() => Tokens.Any(t => t.IsValid());

    public IReadOnlyList<AuthenticationToken> GetValidTokens() => 
        Tokens.Where(t => t.IsValid()).ToList().AsReadOnly();
}

public enum AuthenticationState
{
    Active,
    Deactivated,
    Suspended
}