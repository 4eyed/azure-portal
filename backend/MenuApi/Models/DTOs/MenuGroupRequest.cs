namespace MenuApi.Models.DTOs;

/// <summary>
/// Request DTO for creating/updating menu groups
/// </summary>
public class MenuGroupRequest
{
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}
