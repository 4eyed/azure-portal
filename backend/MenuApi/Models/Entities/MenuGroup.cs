namespace MenuApi.Models.Entities;

/// <summary>
/// Represents a hierarchical menu group
/// </summary>
public class MenuGroup
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;

    // Navigation properties
    public MenuGroup? Parent { get; set; }
    public ICollection<MenuGroup> Children { get; set; } = new List<MenuGroup>();
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
