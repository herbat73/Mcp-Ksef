using Authentication.Domain.Entities;
using Authentication.Domain.ValueObjects;

namespace Authentication.Domain.Repositories;

/// <summary>
/// User repository interface following Microsoft DDD patterns.
/// Repository for UserAggregate (aggregate root) only.
/// No dependencies on infrastructure layer - pure domain interface.
/// </summary>
public interface IUserRepository
{
    Task<UserAggregate?> GetByIdAsync(UserId userId);
    Task<UserAggregate?> GetByTokenIdAsync(string tokenId);
    Task<UserAggregate[]> GetByTenantIdAsync(string tenantId);
    Task SaveAsync(UserAggregate user);
    Task UpdateAsync(UserAggregate user);
    Task DeleteAsync(UserId userId);
    Task<int> CountActiveUsersAsync(string tenantId);
}