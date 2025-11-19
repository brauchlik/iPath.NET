using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.EF.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NotificationQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_queue",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    processed_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_queue", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_queue");
        }
    }
}
