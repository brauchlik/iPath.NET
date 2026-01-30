using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CommunityQuestionnairePriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "questionnaire_community",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "questionnaire_community");
        }
    }
}
