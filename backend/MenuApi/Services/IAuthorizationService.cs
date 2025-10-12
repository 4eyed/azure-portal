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
    /// Checks if a user has admin privileges
    /// </summary>
    Task<bool> IsAdmin(string userId);
}
