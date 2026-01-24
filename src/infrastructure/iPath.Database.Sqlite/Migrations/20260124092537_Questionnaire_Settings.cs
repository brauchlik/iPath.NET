using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Questionnaire_Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionnaireForCommunity_communities_CommunityId",
                table: "QuestionnaireForCommunity");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionnaireForCommunity_questionnaires_QuestionnaireId",
                table: "QuestionnaireForCommunity");

            migrationBuilder.DropPrimaryKey(
                name: "PK_QuestionnaireForCommunity",
                table: "QuestionnaireForCommunity");

            migrationBuilder.DropColumn(
                name: "BodySiteFilter",
                table: "questionnaires");

            migrationBuilder.RenameTable(
                name: "QuestionnaireForCommunity",
                newName: "questionnaire_community");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "questionnaire_community",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_QuestionnaireForCommunity_QuestionnaireId",
                table: "questionnaire_community",
                newName: "IX_questionnaire_community_QuestionnaireId");

            migrationBuilder.RenameIndex(
                name: "IX_QuestionnaireForCommunity_CommunityId",
                table: "questionnaire_community",
                newName: "IX_questionnaire_community_CommunityId");

            migrationBuilder.AddColumn<string>(
                name: "Settings",
                table: "questionnaires",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "BodySiteFilter",
                table: "questionnaire_community",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_questionnaire_community",
                table: "questionnaire_community",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_questionnaire_community_communities_CommunityId",
                table: "questionnaire_community",
                column: "CommunityId",
                principalTable: "communities",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_questionnaire_community_questionnaires_QuestionnaireId",
                table: "questionnaire_community",
                column: "QuestionnaireId",
                principalTable: "questionnaires",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_questionnaire_community_communities_CommunityId",
                table: "questionnaire_community");

            migrationBuilder.DropForeignKey(
                name: "FK_questionnaire_community_questionnaires_QuestionnaireId",
                table: "questionnaire_community");

            migrationBuilder.DropPrimaryKey(
                name: "PK_questionnaire_community",
                table: "questionnaire_community");

            migrationBuilder.DropColumn(
                name: "Settings",
                table: "questionnaires");

            migrationBuilder.DropColumn(
                name: "BodySiteFilter",
                table: "questionnaire_community");

            migrationBuilder.RenameTable(
                name: "questionnaire_community",
                newName: "QuestionnaireForCommunity");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "QuestionnaireForCommunity",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_questionnaire_community_QuestionnaireId",
                table: "QuestionnaireForCommunity",
                newName: "IX_QuestionnaireForCommunity_QuestionnaireId");

            migrationBuilder.RenameIndex(
                name: "IX_questionnaire_community_CommunityId",
                table: "QuestionnaireForCommunity",
                newName: "IX_QuestionnaireForCommunity_CommunityId");

            migrationBuilder.AddColumn<string>(
                name: "BodySiteFilter",
                table: "questionnaires",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_QuestionnaireForCommunity",
                table: "QuestionnaireForCommunity",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionnaireForCommunity_communities_CommunityId",
                table: "QuestionnaireForCommunity",
                column: "CommunityId",
                principalTable: "communities",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionnaireForCommunity_questionnaires_QuestionnaireId",
                table: "QuestionnaireForCommunity",
                column: "QuestionnaireId",
                principalTable: "questionnaires",
                principalColumn: "id");
        }
    }
}
