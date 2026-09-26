using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class StorageMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_migrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    kind = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    scope_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    started_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    purged_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    backup_path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_migrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "storage_migration_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    migration_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    source_instance = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    source_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    target_instance = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    target_key = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    size = table.Column<long>(type: "INTEGER", nullable: true),
                    sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    completed_on = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_migration_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_migration_items_storage_migrations_migration_id",
                        column: x => x.migration_id,
                        principalTable: "storage_migrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_migration_items_document_id",
                table: "storage_migration_items",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_storage_migration_items_migration_id_document_id",
                table: "storage_migration_items",
                columns: new[] { "migration_id", "document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_migrations_status",
                table: "storage_migrations",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_migration_items");

            migrationBuilder.DropTable(
                name: "storage_migrations");
        }
    }
}
