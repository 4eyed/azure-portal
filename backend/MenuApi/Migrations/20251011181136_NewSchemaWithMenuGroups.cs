using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MenuApi.Migrations
{
    /// <inheritdoc />
    public partial class NewSchemaWithMenuGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old MenuItems table if it exists (from previous implementation)
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE dbo.MenuItems;
                END
            ");

            migrationBuilder.CreateTable(
                name: "MenuGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuGroups_MenuGroups_ParentId",
                        column: x => x.ParentId,
                        principalTable: "MenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    MenuGroupId = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuGroups_MenuGroupId",
                        column: x => x.MenuGroupId,
                        principalTable: "MenuGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PowerBIConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    WorkspaceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmbedUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutoRefreshInterval = table.Column<int>(type: "int", nullable: true),
                    DefaultZoom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShowFilterPanel = table.Column<bool>(type: "bit", nullable: false),
                    ShowFilterPanelExpanded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerBIConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PowerBIConfigs_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_MenuGroups_ParentId",
                table: "MenuGroups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_MenuGroupId",
                table: "MenuItems",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerBIConfigs_MenuItemId",
                table: "PowerBIConfigs",
                column: "MenuItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerBIConfigs");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "MenuGroups");
        }
    }
}
