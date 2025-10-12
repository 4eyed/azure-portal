using Microsoft.EntityFrameworkCore;
using MenuApi.Data;
using MenuApi.Models.DTOs;
using MenuApi.Models.Entities;

namespace MenuApi.Services;

/// <summary>
/// Implementation of menu service
/// </summary>
public class MenuService : IMenuService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authService;

    public MenuService(ApplicationDbContext dbContext, IAuthorizationService authService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    public async Task<MenuStructureResponse> GetMenuStructure(string userId)
    {
        var groups = await _dbContext.MenuGroups
            .Include(g => g.MenuItems)
            .ThenInclude(i => i.PowerBIConfig)
            .Where(g => g.IsVisible)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync();

        var result = new MenuStructureResponse();

        foreach (var group in groups)
        {
            var accessibleItems = new List<MenuItemDto>();

            foreach (var item in group.MenuItems.Where(i => i.IsVisible).OrderBy(i => i.DisplayOrder))
            {
                if (await _authService.CanViewMenuItem(userId, item.Name))
                {
                    accessibleItems.Add(new MenuItemDto
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Icon = item.Icon,
                        Url = item.Url,
                        Description = item.Description,
                        Type = item.Type.ToString(),
                        PowerBIConfig = item.PowerBIConfig != null ? new PowerBIConfigDto
                        {
                            WorkspaceId = item.PowerBIConfig.WorkspaceId,
                            ReportId = item.PowerBIConfig.ReportId,
                            EmbedUrl = item.PowerBIConfig.EmbedUrl,
                            AutoRefreshInterval = item.PowerBIConfig.AutoRefreshInterval,
                            DefaultZoom = item.PowerBIConfig.DefaultZoom,
                            ShowFilterPanel = item.PowerBIConfig.ShowFilterPanel,
                            ShowFilterPanelExpanded = item.PowerBIConfig.ShowFilterPanelExpanded
                        } : null
                    });
                }
            }

            if (accessibleItems.Count > 0)
            {
                result.MenuGroups.Add(new MenuGroupDto
                {
                    Id = group.Id,
                    Name = group.Name,
                    Icon = group.Icon,
                    Items = accessibleItems
                });
            }
        }

        return result;
    }

    public async Task<MenuItemDto> CreateMenuItem(MenuItemRequest request)
    {
        if (!Enum.TryParse<MenuItemType>(request.Type, out var menuItemType))
        {
            throw new ArgumentException($"Invalid menu item type: {request.Type}");
        }

        var menuItem = new MenuItem
        {
            Name = request.Name,
            Icon = request.Icon,
            Url = request.Url,
            Description = request.Description,
            Type = menuItemType,
            MenuGroupId = request.MenuGroupId,
            DisplayOrder = request.DisplayOrder,
            IsVisible = request.IsVisible
        };

        _dbContext.MenuItems.Add(menuItem);
        await _dbContext.SaveChangesAsync();

        // Add PowerBI config if provided
        if (request.PowerBIConfig != null)
        {
            var powerBIConfig = new PowerBIConfig
            {
                MenuItemId = menuItem.Id,
                WorkspaceId = request.PowerBIConfig.WorkspaceId,
                ReportId = request.PowerBIConfig.ReportId,
                EmbedUrl = request.PowerBIConfig.EmbedUrl,
                AutoRefreshInterval = request.PowerBIConfig.AutoRefreshInterval,
                DefaultZoom = request.PowerBIConfig.DefaultZoom,
                ShowFilterPanel = request.PowerBIConfig.ShowFilterPanel,
                ShowFilterPanelExpanded = request.PowerBIConfig.ShowFilterPanelExpanded
            };

            _dbContext.PowerBIConfigs.Add(powerBIConfig);
            await _dbContext.SaveChangesAsync();

            menuItem.PowerBIConfig = powerBIConfig;
        }

        return new MenuItemDto
        {
            Id = menuItem.Id,
            Name = menuItem.Name,
            Icon = menuItem.Icon,
            Url = menuItem.Url,
            Description = menuItem.Description,
            Type = menuItem.Type.ToString(),
            PowerBIConfig = menuItem.PowerBIConfig != null ? new PowerBIConfigDto
            {
                WorkspaceId = menuItem.PowerBIConfig.WorkspaceId,
                ReportId = menuItem.PowerBIConfig.ReportId,
                EmbedUrl = menuItem.PowerBIConfig.EmbedUrl,
                AutoRefreshInterval = menuItem.PowerBIConfig.AutoRefreshInterval,
                DefaultZoom = menuItem.PowerBIConfig.DefaultZoom,
                ShowFilterPanel = menuItem.PowerBIConfig.ShowFilterPanel,
                ShowFilterPanelExpanded = menuItem.PowerBIConfig.ShowFilterPanelExpanded
            } : null
        };
    }

    public async Task<MenuItemDto?> UpdateMenuItem(int id, MenuItemRequest request)
    {
        var menuItem = await _dbContext.MenuItems
            .Include(m => m.PowerBIConfig)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (menuItem == null)
        {
            return null;
        }

        if (!Enum.TryParse<MenuItemType>(request.Type, out var menuItemType))
        {
            throw new ArgumentException($"Invalid menu item type: {request.Type}");
        }

        menuItem.Name = request.Name;
        menuItem.Icon = request.Icon;
        menuItem.Url = request.Url;
        menuItem.Description = request.Description;
        menuItem.Type = menuItemType;

        // Only update MenuGroupId if provided
        if (request.MenuGroupId.HasValue)
        {
            // Validate that the MenuGroup exists
            var menuGroupExists = await _dbContext.MenuGroups.AnyAsync(mg => mg.Id == request.MenuGroupId.Value);
            if (!menuGroupExists)
            {
                throw new ArgumentException($"MenuGroup with ID {request.MenuGroupId.Value} does not exist");
            }
            menuItem.MenuGroupId = request.MenuGroupId.Value;
        }

        menuItem.DisplayOrder = request.DisplayOrder;
        menuItem.IsVisible = request.IsVisible;

        // Update PowerBI config
        if (request.PowerBIConfig != null)
        {
            if (menuItem.PowerBIConfig == null)
            {
                menuItem.PowerBIConfig = new PowerBIConfig
                {
                    MenuItemId = menuItem.Id,
                    WorkspaceId = request.PowerBIConfig.WorkspaceId,
                    ReportId = request.PowerBIConfig.ReportId,
                    EmbedUrl = request.PowerBIConfig.EmbedUrl,
                    AutoRefreshInterval = request.PowerBIConfig.AutoRefreshInterval,
                    DefaultZoom = request.PowerBIConfig.DefaultZoom,
                    ShowFilterPanel = request.PowerBIConfig.ShowFilterPanel,
                    ShowFilterPanelExpanded = request.PowerBIConfig.ShowFilterPanelExpanded
                };
                _dbContext.PowerBIConfigs.Add(menuItem.PowerBIConfig);
            }
            else
            {
                menuItem.PowerBIConfig.WorkspaceId = request.PowerBIConfig.WorkspaceId;
                menuItem.PowerBIConfig.ReportId = request.PowerBIConfig.ReportId;
                menuItem.PowerBIConfig.EmbedUrl = request.PowerBIConfig.EmbedUrl;
                menuItem.PowerBIConfig.AutoRefreshInterval = request.PowerBIConfig.AutoRefreshInterval;
                menuItem.PowerBIConfig.DefaultZoom = request.PowerBIConfig.DefaultZoom;
                menuItem.PowerBIConfig.ShowFilterPanel = request.PowerBIConfig.ShowFilterPanel;
                menuItem.PowerBIConfig.ShowFilterPanelExpanded = request.PowerBIConfig.ShowFilterPanelExpanded;
            }
        }
        else if (menuItem.PowerBIConfig != null)
        {
            _dbContext.PowerBIConfigs.Remove(menuItem.PowerBIConfig);
            menuItem.PowerBIConfig = null;
        }

        await _dbContext.SaveChangesAsync();

        return new MenuItemDto
        {
            Id = menuItem.Id,
            Name = menuItem.Name,
            Icon = menuItem.Icon,
            Url = menuItem.Url,
            Description = menuItem.Description,
            Type = menuItem.Type.ToString(),
            PowerBIConfig = menuItem.PowerBIConfig != null ? new PowerBIConfigDto
            {
                WorkspaceId = menuItem.PowerBIConfig.WorkspaceId,
                ReportId = menuItem.PowerBIConfig.ReportId,
                EmbedUrl = menuItem.PowerBIConfig.EmbedUrl,
                AutoRefreshInterval = menuItem.PowerBIConfig.AutoRefreshInterval,
                DefaultZoom = menuItem.PowerBIConfig.DefaultZoom,
                ShowFilterPanel = menuItem.PowerBIConfig.ShowFilterPanel,
                ShowFilterPanelExpanded = menuItem.PowerBIConfig.ShowFilterPanelExpanded
            } : null
        };
    }

    public async Task<bool> DeleteMenuItem(int id)
    {
        var menuItem = await _dbContext.MenuItems.FindAsync(id);

        if (menuItem == null)
        {
            return false;
        }

        _dbContext.MenuItems.Remove(menuItem);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<MenuGroupDto> CreateMenuGroup(MenuGroupRequest request)
    {
        var menuGroup = new MenuGroup
        {
            Name = request.Name,
            Icon = request.Icon,
            ParentId = request.ParentId,
            DisplayOrder = request.DisplayOrder,
            IsVisible = request.IsVisible
        };

        _dbContext.MenuGroups.Add(menuGroup);
        await _dbContext.SaveChangesAsync();

        return new MenuGroupDto
        {
            Id = menuGroup.Id,
            Name = menuGroup.Name,
            Icon = menuGroup.Icon,
            Items = new List<MenuItemDto>()
        };
    }

    public async Task<MenuGroupDto?> UpdateMenuGroup(int id, MenuGroupRequest request)
    {
        var menuGroup = await _dbContext.MenuGroups.FindAsync(id);

        if (menuGroup == null)
        {
            return null;
        }

        menuGroup.Name = request.Name;
        menuGroup.Icon = request.Icon;
        menuGroup.ParentId = request.ParentId;
        menuGroup.DisplayOrder = request.DisplayOrder;
        menuGroup.IsVisible = request.IsVisible;

        await _dbContext.SaveChangesAsync();

        return new MenuGroupDto
        {
            Id = menuGroup.Id,
            Name = menuGroup.Name,
            Icon = menuGroup.Icon,
            Items = new List<MenuItemDto>()
        };
    }

    public async Task<bool> DeleteMenuGroup(int id)
    {
        var menuGroup = await _dbContext.MenuGroups.FindAsync(id);

        if (menuGroup == null)
        {
            return false;
        }

        _dbContext.MenuGroups.Remove(menuGroup);
        await _dbContext.SaveChangesAsync();

        return true;
    }
}
