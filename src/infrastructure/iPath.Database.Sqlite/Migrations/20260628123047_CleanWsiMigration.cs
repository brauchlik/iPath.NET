using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CleanWsiMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS vsi_conversion_jobs");
            migrationBuilder.Sql("DROP TABLE IF EXISTS wsi_conversion_jobs");

            migrationBuilder.AddColumn<DateTime>(
                name: "PurgedOn",
                table: "documents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "wsi_conversion_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    started_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    completed_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    original_storage_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    converted_storage_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    plugin_type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wsi_conversion_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_wsi_conversion_jobs_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wsi_conversion_jobs_document_id",
                table: "wsi_conversion_jobs",
                column: "document_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS wsi_conversion_jobs");

            migrationBuilder.DropColumn(
                name: "PurgedOn",
                table: "documents");

            migrationBuilder.CreateTable(
                name: "vsi_conversion_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    document_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    completed_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    converted_storage_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    error_message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    original_storage_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    retry_count = table.Column<int>(type: "INTEGER", nullable: false),
                    started_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vsi_conversion_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_vsi_conversion_jobs_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vsi_conversion_jobs_document_id",
                table: "vsi_conversion_jobs",
                column: "document_id",
                unique: true);
        }
    }
}
