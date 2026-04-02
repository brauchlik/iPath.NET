using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmailImportLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EmailImportLogs",
                table: "EmailImportLogs");

            migrationBuilder.RenameTable(
                name: "EmailImportLogs",
                newName: "email_import_logs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_email_import_logs",
                table: "email_import_logs",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_email_import_logs",
                table: "email_import_logs");

            migrationBuilder.RenameTable(
                name: "email_import_logs",
                newName: "EmailImportLogs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmailImportLogs",
                table: "EmailImportLogs",
                column: "Id");
        }
    }
}
