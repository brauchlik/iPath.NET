using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddVsiConversionJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_documents_servicerequest_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_annotations_servicerequest_id",
                table: "annotations");

            migrationBuilder.CreateTable(
                name: "vsi_conversion_jobs",
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
                    converted_storage_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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
                name: "IX_documents_servicerequest_id_DeletedOn",
                table: "documents",
                columns: new[] { "servicerequest_id", "DeletedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_annotations_servicerequest_id_DeletedOn",
                table: "annotations",
                columns: new[] { "servicerequest_id", "DeletedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_vsi_conversion_jobs_document_id",
                table: "vsi_conversion_jobs",
                column: "document_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vsi_conversion_jobs");

            migrationBuilder.DropIndex(
                name: "IX_documents_servicerequest_id_DeletedOn",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_annotations_servicerequest_id_DeletedOn",
                table: "annotations");

            migrationBuilder.CreateIndex(
                name: "IX_documents_servicerequest_id",
                table: "documents",
                column: "servicerequest_id");

            migrationBuilder.CreateIndex(
                name: "IX_annotations_servicerequest_id",
                table: "annotations",
                column: "servicerequest_id");
        }
    }
}
