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

        // No seed data - admins will create menu structure via Admin Mode
    }
}
