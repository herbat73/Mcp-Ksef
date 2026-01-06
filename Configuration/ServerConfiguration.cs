namespace Configuration;

/// <summary>
/// Server configuration settings from appsettings.json
/// </summary>
public class ServerConfiguration
{
    public const string SectionName = "Server";
    
    /// <summary>
    /// Port number for the server to listen on
    /// </summary>
    public int Port { get; set; } = 3001;
    
    /// <summary>
    /// Host address to bind to (0.0.0.0 for all interfaces, localhost for local only)
    /// </summary>
    public string Host { get; set; } = "0.0.0.0";
    
    /// <summary>
    /// Gets the complete URL for the server
    /// </summary>
    public string GetUrl(string scheme = "http")
    {
        return $"{scheme}://{Host}:{Port}";
    }
}