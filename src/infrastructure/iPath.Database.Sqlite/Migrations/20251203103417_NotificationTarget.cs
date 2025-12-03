using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.EF.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NotificationTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "type",
                table: "notification_queue",
                newName: "target");

            migrationBuilder.AddColumn<bool>(
                name: "daily_summary",
                table: "notification_queue",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "daily_summary",
                table: "notification_queue");

            migrationBuilder.RenameColumn(
                name: "target",
                table: "notification_queue",
                newName: "type");
        }
    }
}
