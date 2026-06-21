using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAiExtractionPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_correction_deltas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    WrongPrediction = table.Column<string>(type: "TEXT", nullable: true),
                    CorrectedValue = table.Column<string>(type: "TEXT", nullable: true),
                    ContextualSnippet = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_correction_deltas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "case_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VectorData = table.Column<byte[]>(type: "BLOB", nullable: true),
                    ModelIdentifierUsed = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_embeddings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "case_ingestion_lineages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RawInputText = table.Column<string>(type: "TEXT", nullable: false),
                    AiSuggestedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    HumanAcceptedDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    ModelIdentifierUsed = table.Column<string>(type: "TEXT", nullable: true),
                    WasOverridden = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasBeenAnalyzedBySupervisor = table.Column<bool>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_ingestion_lineages", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_correction_deltas");

            migrationBuilder.DropTable(
                name: "case_embeddings");

            migrationBuilder.DropTable(
                name: "case_ingestion_lineages");
        }
    }
}
