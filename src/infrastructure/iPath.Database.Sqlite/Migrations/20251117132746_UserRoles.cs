using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.EF.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "roles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_user_id",
                table: "roles",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_roles_users_user_id",
                table: "roles",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_roles_users_user_id",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_user_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "roles");
        }
    }
}
