namespace MenuApi.Models.DTOs;

/// <summary>
/// Request DTO for creating/updating menu items
/// </summary>
public class MenuItemRequest
{
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public required string Url { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; }
    public int? MenuGroupId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public PowerBIConfigRequest? PowerBIConfig { get; set; }
}

public class PowerBIConfigRequest
{
    public required string WorkspaceId { get; set; }
    public required string ReportId { get; set; }
    public required string EmbedUrl { get; set; }
    public int? AutoRefreshInterval { get; set; }
    public string? DefaultZoom { get; set; }
    public bool ShowFilterPanel { get; set; } = true;
    public bool ShowFilterPanelExpanded { get; set; } = false;
}
