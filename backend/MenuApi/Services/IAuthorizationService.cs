namespace MenuApi.Services;

/// <summary>
/// Service for handling OpenFGA authorization checks
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Checks if a user can view a menu item
    /// </summary>
    Task<bool> CanViewMenuItem(string userId, string menuItemName);

    /// <summary>
    /// Batch checks if a user can view multiple menu items
    /// </summary>
    /// <returns>Dictionary with menu item names as keys and authorization results as values</returns>
    Task<Dictionary<string, bool>> CanViewMenuItems(string userId, IEnumerable<string> menuItemNames);

    /// <summary>
    /// Checks if a user has admin privileges
    /// </summary>
    Task<bool> IsAdmin(string userId);
}
