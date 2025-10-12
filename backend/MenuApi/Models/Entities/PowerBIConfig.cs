namespace MenuApi.Models.Entities;

/// <summary>
/// Configuration for Power BI report embedding
/// </summary>
public class PowerBIConfig
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public required string WorkspaceId { get; set; }
    public required string ReportId { get; set; }
    public required string EmbedUrl { get; set; }
    public int? AutoRefreshInterval { get; set; }
    public string? DefaultZoom { get; set; }
    public bool ShowFilterPanel { get; set; } = true;
    public bool ShowFilterPanelExpanded { get; set; } = false;

    // Navigation property
    public MenuItem? MenuItem { get; set; }
}
