using KSeF.Client.Core.Models.Authorization;

namespace McpKsef.HybridApp.Services;

public interface IKsefAuthorizationService
{
    Task VerifyAuthToken(CancellationToken cancellationToken);
    AuthenticationOperationStatusResponse GetAuthenticationInfo();
}