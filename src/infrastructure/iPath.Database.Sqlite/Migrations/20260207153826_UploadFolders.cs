using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UploadFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "useruploadfolder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageProvider = table.Column<string>(type: "TEXT", nullable: false),
                    StorageId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_useruploadfolder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_useruploadfolder_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "servicerequestuploadfolder",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UploadFolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicerequestuploadfolder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_servicerequestuploadfolder_servicerequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "servicerequests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_servicerequestuploadfolder_useruploadfolder_UploadFolderId",
                        column: x => x.UploadFolderId,
                        principalTable: "useruploadfolder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_servicerequestuploadfolder_ServiceRequestId",
                table: "servicerequestuploadfolder",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_servicerequestuploadfolder_UploadFolderId",
                table: "servicerequestuploadfolder",
                column: "UploadFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_useruploadfolder_UserId",
                table: "useruploadfolder",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "servicerequestuploadfolder");

            migrationBuilder.DropTable(
                name: "useruploadfolder");
        }
    }
}
