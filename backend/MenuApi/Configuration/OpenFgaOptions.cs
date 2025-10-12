namespace MenuApi.Configuration;

/// <summary>
/// Configuration options for OpenFGA authorization service
/// </summary>
public class OpenFgaOptions
{
    public const string SectionName = "OpenFga";

    /// <summary>
    /// OpenFGA API URL (e.g., http://localhost:8080)
    /// </summary>
    public string ApiUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// OpenFGA Store ID
    /// </summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiUrl))
            throw new InvalidOperationException("OpenFGA ApiUrl is required");

        if (string.IsNullOrWhiteSpace(StoreId))
            throw new InvalidOperationException("OpenFGA StoreId is required");
    }
}
