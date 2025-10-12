using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MenuApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "MenuItems",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "MenuGroups",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "MenuGroups",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "MenuGroups",
                keyColumn: "Id",
                keyValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "MenuGroups",
                columns: new[] { "Id", "DisplayOrder", "Icon", "IsVisible", "Name", "ParentId" },
                values: new object[,]
                {
                    { 1, 1, "📦", true, "CLIENT PRODUCT", null },
                    { 2, 2, "📊", true, "CLIENT REPORTING", null },
                    { 3, 3, "🎯", true, "PGIM DEMO", null }
                });

            migrationBuilder.InsertData(
                table: "MenuItems",
                columns: new[] { "Id", "Description", "DisplayOrder", "Icon", "IsVisible", "MenuGroupId", "Name", "Type", "Url" },
                values: new object[,]
                {
                    { 1, "View your dashboard", 1, "📊", true, 1, "Dashboard", 0, "/dashboard" },
                    { 2, "Manage users", 2, "👥", true, 1, "Users", 0, "/users" },
                    { 3, "Application settings", 3, "⚙️", true, 1, "Settings", 0, "/settings" },
                    { 4, "View and generate reports", 1, "📈", true, 2, "Reports", 0, "/reports" },
                    { 5, "Power BI Risk Dashboard", 1, "⚠️", true, 3, "Risk Dashboard", 1, "/powerbi/risk" }
                });
        }
    }
}
