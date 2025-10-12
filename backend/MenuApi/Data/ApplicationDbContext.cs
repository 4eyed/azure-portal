using Microsoft.EntityFrameworkCore;
using MenuApi.Models.Entities;

namespace MenuApi.Data;

/// <summary>
/// Application database context
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MenuItem> MenuItems { get; set; } = null!;
    public DbSet<MenuGroup> MenuGroups { get; set; } = null!;
    public DbSet<PowerBIConfig> PowerBIConfigs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure MenuGroup self-referencing relationship
        modelBuilder.Entity<MenuGroup>()
            .HasOne(g => g.Parent)
            .WithMany(g => g.Children)
            .HasForeignKey(g => g.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure MenuItem-MenuGroup relationship
        modelBuilder.Entity<MenuItem>()
            .HasOne(m => m.MenuGroup)
            .WithMany(g => g.MenuItems)
            .HasForeignKey(m => m.MenuGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure PowerBIConfig-MenuItem relationship
        modelBuilder.Entity<PowerBIConfig>()
            .HasOne(c => c.MenuItem)
            .WithOne(m => m.PowerBIConfig)
            .HasForeignKey<PowerBIConfig>(c => c.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed data for menu groups
        modelBuilder.Entity<MenuGroup>().HasData(
            new MenuGroup { Id = 1, Name = "CLIENT PRODUCT", Icon = "📦", DisplayOrder = 1 },
            new MenuGroup { Id = 2, Name = "CLIENT REPORTING", Icon = "📊", DisplayOrder = 2 },
            new MenuGroup { Id = 3, Name = "PGIM DEMO", Icon = "🎯", DisplayOrder = 3 }
        );

        // Seed data for menu items
        modelBuilder.Entity<MenuItem>().HasData(
            new MenuItem { Id = 1, Name = "Dashboard", Icon = "📊", Url = "/dashboard", Description = "View your dashboard", Type = MenuItemType.AppComponent, MenuGroupId = 1, DisplayOrder = 1 },
            new MenuItem { Id = 2, Name = "Users", Icon = "👥", Url = "/users", Description = "Manage users", Type = MenuItemType.AppComponent, MenuGroupId = 1, DisplayOrder = 2 },
            new MenuItem { Id = 3, Name = "Settings", Icon = "⚙️", Url = "/settings", Description = "Application settings", Type = MenuItemType.AppComponent, MenuGroupId = 1, DisplayOrder = 3 },
            new MenuItem { Id = 4, Name = "Reports", Icon = "📈", Url = "/reports", Description = "View and generate reports", Type = MenuItemType.AppComponent, MenuGroupId = 2, DisplayOrder = 1 },
            new MenuItem { Id = 5, Name = "Risk Dashboard", Icon = "⚠️", Url = "/powerbi/risk", Description = "Power BI Risk Dashboard", Type = MenuItemType.PowerBIReport, MenuGroupId = 3, DisplayOrder = 1 }
        );
    }
}
