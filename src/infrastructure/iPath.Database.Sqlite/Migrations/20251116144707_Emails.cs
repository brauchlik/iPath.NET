using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.EF.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Emails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "message",
                table: "notification_queue",
                newName: "user_id");

            migrationBuilder.AddColumn<string>(
                name: "data",
                table: "notification_queue",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "temp_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    receiver = table.Column<string>(type: "TEXT", nullable: false),
                    subject = table.Column<string>(type: "TEXT", nullable: false),
                    body = table.Column<string>(type: "TEXT", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    sent_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_temp_emails", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_queue_user_id",
                table: "notification_queue",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notification_queue_users_user_id",
                table: "notification_queue",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notification_queue_users_user_id",
                table: "notification_queue");

            migrationBuilder.DropTable(
                name: "temp_emails");

            migrationBuilder.DropIndex(
                name: "ix_notification_queue_user_id",
                table: "notification_queue");

            migrationBuilder.DropColumn(
                name: "data",
                table: "notification_queue");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "notification_queue",
                newName: "message");
        }
    }
}
