using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UploadFolderRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_servicerequestuploadfolder_useruploadfolder_UploadFolderId",
                table: "servicerequestuploadfolder");

            migrationBuilder.AddForeignKey(
                name: "FK_servicerequestuploadfolder_useruploadfolder_UploadFolderId",
                table: "servicerequestuploadfolder",
                column: "UploadFolderId",
                principalTable: "useruploadfolder",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_servicerequestuploadfolder_useruploadfolder_UploadFolderId",
                table: "servicerequestuploadfolder");

            migrationBuilder.AddForeignKey(
                name: "FK_servicerequestuploadfolder_useruploadfolder_UploadFolderId",
                table: "servicerequestuploadfolder",
                column: "UploadFolderId",
                principalTable: "useruploadfolder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
