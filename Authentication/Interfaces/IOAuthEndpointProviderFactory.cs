namespace Authentication.Interfaces;

/// <summary>
/// Factory interface for creating OAuth endpoint providers.
/// Follows SOLID Dependency Inversion Principle.
/// </summary>
public interface IOAuthEndpointProviderFactory
{
    /// <summary>
    /// Gets the appropriate OAuth endpoint provider based on current configuration.
    /// </summary>
    IOAuthEndpointProvider GetProvider();
}