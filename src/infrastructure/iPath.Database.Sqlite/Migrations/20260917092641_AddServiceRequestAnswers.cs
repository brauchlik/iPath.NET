using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_request_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    service_request_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestionnaireId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    QuestionnaireVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CodeSystem = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CodeDisplay = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OtherCodings = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ValueType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ValueDisplay = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExtractionVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_request_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_request_answers_servicerequests_service_request_id",
                        column: x => x.service_request_id,
                        principalTable: "servicerequests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_request_answers_Code",
                table: "service_request_answers",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_service_request_answers_QuestionnaireId",
                table: "service_request_answers",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_service_request_answers_service_request_id",
                table: "service_request_answers",
                column: "service_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_request_answers");
        }
    }
}
