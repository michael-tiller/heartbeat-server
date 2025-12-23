namespace Heartbeat.Server.Middleware;

/// <summary>
/// Configuration for API key authentication.
/// </summary>
public sealed class ApiKeySettings
{
    public const string SectionName = "ApiKey";
    
    /// <summary>
    /// Whether API key authentication is enabled.
    /// Defaults to false in Development, true in Production.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Valid API keys. At least one must be configured when enabled.
    /// </summary>
    public List<string> Keys { get; set; } = new();
    
    public bool IsValidApiKey(string apiKey)
    {
        return Keys.Any(k => string.Equals(k, apiKey, StringComparison.Ordinal));
    }
}
