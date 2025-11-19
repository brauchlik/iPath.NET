using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.EF.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class User_iPath2Username : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ipath2_username",
                table: "users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ipath2_username",
                table: "users");
        }
    }
}
