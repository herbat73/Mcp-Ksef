namespace McpKsef.HybridApp.Tests;

/// <summary>
/// Tests that change process-wide state (environment variables, Console.Out) must belong to this
/// collection. xUnit runs it on its own, after all parallel tests, so those tests cannot see each
/// other's changes.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class EnvironmentVariablesCollection
{
    public const string Name = "Environment variables";
}
