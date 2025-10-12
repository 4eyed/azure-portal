namespace MenuApi.Models.DTOs;

/// <summary>
/// Response DTO for Power BI workspaces
/// </summary>
public class PowerBIWorkspaceResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}
