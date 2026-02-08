using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class StorageIdRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageId",
                table: "servicerequests");

            migrationBuilder.DropColumn(
                name: "StorageId",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "StorageId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "StorageId",
                table: "communities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageId",
                table: "servicerequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageId",
                table: "groups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageId",
                table: "documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageId",
                table: "communities",
                type: "TEXT",
                nullable: true);
        }
    }
}
