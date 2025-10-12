using MenuApi.Models.DTOs;

namespace MenuApi.Services;

/// <summary>
/// Service for managing menu items and structure
/// </summary>
public interface IMenuService
{
    /// <summary>
    /// Gets the menu structure for a specific user (filtered by permissions)
    /// </summary>
    Task<MenuStructureResponse> GetMenuStructure(string userId);

    /// <summary>
    /// Creates a new menu item
    /// </summary>
    Task<MenuItemDto> CreateMenuItem(MenuItemRequest request);

    /// <summary>
    /// Updates an existing menu item
    /// </summary>
    Task<MenuItemDto?> UpdateMenuItem(int id, MenuItemRequest request);

    /// <summary>
    /// Deletes a menu item
    /// </summary>
    Task<bool> DeleteMenuItem(int id);

    /// <summary>
    /// Creates a new menu group
    /// </summary>
    Task<MenuGroupDto> CreateMenuGroup(MenuGroupRequest request);

    /// <summary>
    /// Updates an existing menu group
    /// </summary>
    Task<MenuGroupDto?> UpdateMenuGroup(int id, MenuGroupRequest request);

    /// <summary>
    /// Deletes a menu group
    /// </summary>
    Task<bool> DeleteMenuGroup(int id);
}
