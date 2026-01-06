using Authentication.Domain.Entities;
using Authentication.Domain.Repositories;
using Authentication.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Authentication.Infrastructure.Repositories;

/// <summary>
/// In-memory user repository following Microsoft DDD patterns.
/// Infrastructure implementation of domain repository interface.
/// </summary>
public class InMemoryUserRepository : IUserRepository
{
    private readonly ILogger<InMemoryUserRepository> _logger;
    private readonly ConcurrentDictionary<string, UserAggregate> _users = new();
    private readonly ConcurrentDictionary<string, string> _tokenToUserId = new(); // Token ID -> User ID mapping

    public InMemoryUserRepository(ILogger<InMemoryUserRepository> logger)
    {
        _logger = logger;
    }

    public Task<UserAggregate?> GetByIdAsync(UserId userId)
    {
        _users.TryGetValue(userId.Value, out var user);
        return Task.FromResult(user);
    }

    public Task<UserAggregate?> GetByTokenIdAsync(string tokenId)
    {
        if (_tokenToUserId.TryGetValue(tokenId, out var userId))
        {
            return GetByIdAsync(UserId.Create(userId));
        }
        return Task.FromResult<UserAggregate?>(null);
    }

    public Task<UserAggregate[]> GetByTenantIdAsync(string tenantId)
    {
        var tenantUsers = _users.Values
            .Where(u => u.TenantId == tenantId)
            .ToArray();
        return Task.FromResult(tenantUsers);
    }

    public Task SaveAsync(UserAggregate user)
    {
        _users.TryAdd(user.Id.Value, user);
        UpdateTokenMappings(user);
        
        _logger.LogDebug("Saved user {UserId} with {TokenCount} tokens", 
            user.Id.Value, user.Tokens.Count);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserAggregate user)
    {
        _users.TryUpdate(user.Id.Value, user, _users[user.Id.Value]);
        UpdateTokenMappings(user);
        
        _logger.LogDebug("Updated user {UserId} with {TokenCount} tokens", 
            user.Id.Value, user.Tokens.Count);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(UserId userId)
    {
        if (_users.TryRemove(userId.Value, out var user))
        {
            // Remove token mappings
            foreach (var token in user.Tokens)
            {
                _tokenToUserId.TryRemove(token.TokenId, out _);
            }
            _logger.LogDebug("Deleted user {UserId}", userId.Value);
        }
        return Task.CompletedTask;
    }

    public Task<int> CountActiveUsersAsync(string tenantId)
    {
        int activeCount = _users.Values
            .Count(u => u.TenantId == tenantId && u.State == AuthenticationState.Active);
        return Task.FromResult(activeCount);
    }

    private void UpdateTokenMappings(UserAggregate user)
    {
        // Update token to user mappings for efficient token lookups
        foreach (var token in user.Tokens)
        {
            _tokenToUserId.TryAdd(token.TokenId, user.Id.Value);
        }
    }
}