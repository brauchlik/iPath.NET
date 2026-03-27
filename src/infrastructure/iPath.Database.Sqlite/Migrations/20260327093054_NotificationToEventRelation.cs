using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NotificationToEventRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_notifications_EventId",
                table: "notifications",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_eventstore_EventId",
                table: "notifications",
                column: "EventId",
                principalTable: "eventstore",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_eventstore_EventId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_EventId",
                table: "notifications");
        }
    }
}
