namespace MenuApi.Models.Entities;

/// <summary>
/// Represents a menu item in the navigation
/// </summary>
public class MenuItem
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public required string Url { get; set; }
    public string? Description { get; set; }
    public MenuItemType Type { get; set; } = MenuItemType.AppComponent;
    public int? MenuGroupId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;

    // Navigation properties
    public MenuGroup? MenuGroup { get; set; }
    public PowerBIConfig? PowerBIConfig { get; set; }
}
