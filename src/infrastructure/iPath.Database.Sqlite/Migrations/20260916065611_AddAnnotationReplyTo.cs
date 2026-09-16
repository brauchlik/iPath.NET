using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnotationReplyTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reply_to_id",
                table: "annotations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_annotations_reply_to_id",
                table: "annotations",
                column: "reply_to_id");

            migrationBuilder.AddForeignKey(
                name: "FK_annotations_annotations_reply_to_id",
                table: "annotations",
                column: "reply_to_id",
                principalTable: "annotations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_annotations_annotations_reply_to_id",
                table: "annotations");

            migrationBuilder.DropIndex(
                name: "IX_annotations_reply_to_id",
                table: "annotations");

            migrationBuilder.DropColumn(
                name: "reply_to_id",
                table: "annotations");
        }
    }
}
