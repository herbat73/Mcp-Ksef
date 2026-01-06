using Authentication.Domain.Entities;
using Authentication.Domain.ValueObjects;
using Authentication.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Authentication.Domain.Services;

/// <summary>
/// Authentication domain service following Microsoft DDD patterns.
/// Contains business logic that doesn't belong to a single aggregate.
/// NO dependencies on infrastructure layer.
/// </summary>
public interface IAuthenticationDomainService
{
    Task<AuthenticationToken> IssueTokenAsync(UserId userId, string clientId, string[] scopes, ValueObjects.TokenType type, TimeSpan lifetime);
    Task<bool> ValidateTokenAsync(string tokenId);
    Task<bool> RevokeTokenAsync(string tokenId);
    Task<UserAggregate> CreateUserAsync(string userId, string tenantId);
}

public class AuthenticationDomainService : IAuthenticationDomainService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthenticationDomainService> _logger;

    public AuthenticationDomainService(
        IUserRepository userRepository,
        ILogger<AuthenticationDomainService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<AuthenticationToken> IssueTokenAsync(UserId userId, string clientId, string[] scopes, ValueObjects.TokenType type, TimeSpan lifetime)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for token issuance", userId.Value);
            throw new ArgumentException($"User {userId.Value} not found");
        }

        var token = user.IssueToken(clientId, scopes, lifetime, type);
        await _userRepository.UpdateAsync(user);

        _logger.LogDebug("Issued {TokenType} token {TokenId} for user {UserId}",
            type, token.TokenId, userId.Value);

        return token;
    }

    public async Task<bool> ValidateTokenAsync(string tokenId)
    {
        var user = await _userRepository.GetByTokenIdAsync(tokenId);
        if (user == null)
        {
            _logger.LogDebug("No user found for token {TokenId}", tokenId);
            return false;
        }

        var token = user.Tokens.FirstOrDefault(t => t.TokenId == tokenId);
        bool isValid = token?.IsValid() == true;

        if (!isValid)
        {
            _logger.LogDebug("Token {TokenId} is invalid or expired", tokenId);
        }

        return isValid;
    }

    public async Task<bool> RevokeTokenAsync(string tokenId)
    {
        try
        {
            var user = await _userRepository.GetByTokenIdAsync(tokenId);
            if (user == null)
            {
                _logger.LogWarning("No user found for token revocation {TokenId}", tokenId);
                return false;
            }

            user.RevokeToken(tokenId);
            await _userRepository.UpdateAsync(user);

            _logger.LogInformation("Token {TokenId} revoked successfully", tokenId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke token {TokenId}", tokenId);
            return false;
        }
    }

    public async Task<UserAggregate> CreateUserAsync(string userId, string tenantId)
    {
        var id = UserId.Create(userId);
        var existingUser = await _userRepository.GetByIdAsync(id);
        
        if (existingUser != null)
        {
            _logger.LogDebug("User {UserId} already exists", userId);
            return existingUser;
        }

        var user = UserAggregate.Create(userId, tenantId);
        await _userRepository.SaveAsync(user);

        _logger.LogInformation("Created user {UserId} in tenant {TenantId}", userId, tenantId);
        return user;
    }
}