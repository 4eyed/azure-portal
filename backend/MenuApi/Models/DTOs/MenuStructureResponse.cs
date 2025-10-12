namespace MenuApi.Models.DTOs;

/// <summary>
/// Response DTO for menu structure
/// </summary>
public class MenuStructureResponse
{
    public List<MenuGroupDto> MenuGroups { get; set; } = new();
}

public class MenuGroupDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public List<MenuItemDto> Items { get; set; } = new();
}

public class MenuItemDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public required string Url { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; }
    public PowerBIConfigDto? PowerBIConfig { get; set; }
}

public class PowerBIConfigDto
{
    public required string WorkspaceId { get; set; }
    public required string ReportId { get; set; }
    public required string EmbedUrl { get; set; }
    public int? AutoRefreshInterval { get; set; }
    public string? DefaultZoom { get; set; }
    public bool ShowFilterPanel { get; set; }
    public bool ShowFilterPanelExpanded { get; set; }
}
