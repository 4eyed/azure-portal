using Microsoft.EntityFrameworkCore;
using MenuApi.Models;

namespace MenuApi.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MenuItem> MenuItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed data for menu items
        modelBuilder.Entity<MenuItem>().HasData(
            new MenuItem { Id = 1, Name = "Dashboard", Icon = "📊", Url = "/dashboard", Description = "View your dashboard" },
            new MenuItem { Id = 2, Name = "Users", Icon = "👥", Url = "/users", Description = "Manage users" },
            new MenuItem { Id = 3, Name = "Settings", Icon = "⚙️", Url = "/settings", Description = "Application settings" },
            new MenuItem { Id = 4, Name = "Reports", Icon = "📈", Url = "/reports", Description = "View and generate reports" }
        );
    }
}
