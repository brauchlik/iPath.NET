using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationReadOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadOn",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadOn",
                table: "notifications");
        }
    }
}
