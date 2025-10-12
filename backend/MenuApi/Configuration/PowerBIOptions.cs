namespace MenuApi.Configuration;

/// <summary>
/// Configuration options for Power BI service integration
/// </summary>
public class PowerBIOptions
{
    public const string SectionName = "PowerBI";

    /// <summary>
    /// Azure AD Client ID (Service Principal)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Client Secret (Service Principal)
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Power BI API Scopes
    /// </summary>
    public string[] Scopes { get; set; } = new[] { "https://analysis.windows.net/powerbi/api/.default" };

    /// <summary>
    /// Azure AD Authority URL
    /// </summary>
    public string Authority => $"https://login.microsoftonline.com/{TenantId}";

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("PowerBI ClientId is required");

        if (string.IsNullOrWhiteSpace(ClientSecret))
            throw new InvalidOperationException("PowerBI ClientSecret is required");

        if (string.IsNullOrWhiteSpace(TenantId))
            throw new InvalidOperationException("PowerBI TenantId is required");
    }
}
