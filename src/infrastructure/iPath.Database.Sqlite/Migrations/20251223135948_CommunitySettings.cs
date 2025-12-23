using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CommunitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "description",
                table: "communities");

            migrationBuilder.AddColumn<string>(
                name: "Settings",
                table: "communities",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Settings",
                table: "communities");

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "communities",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "communities",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }
    }
}
