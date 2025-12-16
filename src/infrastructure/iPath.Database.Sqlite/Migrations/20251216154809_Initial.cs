using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventstore",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ObjectName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ObjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventstore", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "NodeImports",
                columns: table => new
                {
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: true),
                    Info = table.Column<string>(type: "TEXT", nullable: true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeImports", x => x.NodeId);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "temp_emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Receiver = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temp_emails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ipath2_username = table.Column<string>(type: "TEXT", nullable: true),
                    ipath2_password = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    profile = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "communities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communities", x => x.id);
                    table.ForeignKey(
                        name: "FK_communities_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageId = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GroupType = table.Column<int>(type: "INTEGER", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Settings = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_groups_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    DailySummary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaires",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestionnaireId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Resource = table.Column<string>(type: "TEXT", nullable: false),
                    DeletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaires", x => x.id);
                    table.ForeignKey(
                        name: "FK_questionnaires_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_user_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    community_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    AllGroups = table.Column<bool>(type: "INTEGER", nullable: false),
                    iPath2_Id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_group_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_group_members_communities_community_id",
                        column: x => x.community_id,
                        principalTable: "communities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_community_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    community_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_groups_communities_community_id",
                        column: x => x.community_id,
                        principalTable: "communities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_groups_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFavourite = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationSource = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationTarget = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationSettings = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_members_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    StorageId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    RootNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentNodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortNr = table.Column<int>(type: "INTEGER", nullable: true),
                    IsDraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    NodeType = table.Column<string>(type: "TEXT", nullable: false),
                    DeletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    file = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_nodes_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_nodes_nodes_RootNodeId",
                        column: x => x.RootNodeId,
                        principalTable: "nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_nodes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestionnaireId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Usage = table.Column<int>(type: "INTEGER", nullable: false),
                    ExplicitVersion = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_questionnaire_groups_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_questionnaire_groups_questionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "questionnaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "annotations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: true),
                    DeletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_annotations", x => x.id);
                    table.ForeignKey(
                        name: "FK_annotations_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_annotations_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "node_lastvisits",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_lastvisits", x => new { x.UserId, x.NodeId });
                    table.ForeignKey(
                        name: "FK_node_lastvisits_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_node_lastvisits_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestionnaireId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnnotationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Resource = table.Column<string>(type: "jsonb", nullable: false),
                    DeletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_responses", x => x.id);
                    table.ForeignKey(
                        name: "FK_questionnaire_responses_annotations_AnnotationId",
                        column: x => x.AnnotationId,
                        principalTable: "annotations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_questionnaire_responses_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_questionnaire_responses_questionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "questionnaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_questionnaire_responses_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_annotations_NodeId",
                table: "annotations",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_annotations_OwnerId",
                table: "annotations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_communities_OwnerId",
                table: "communities",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_community_group_members_community_id",
                table: "community_group_members",
                column: "community_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_group_members_user_id_community_id",
                table: "community_group_members",
                columns: new[] { "user_id", "community_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_groups_community_id",
                table: "community_groups",
                column: "community_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_groups_group_id_community_id",
                table: "community_groups",
                columns: new[] { "group_id", "community_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eventstore_EventDate",
                table: "eventstore",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_eventstore_EventName",
                table: "eventstore",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_eventstore_ObjectId",
                table: "eventstore",
                column: "ObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_eventstore_ObjectName",
                table: "eventstore",
                column: "ObjectName");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_GroupId",
                table: "group_members",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_UserId_GroupId",
                table: "group_members",
                columns: new[] { "UserId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_OwnerId",
                table: "groups",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_node_lastvisits_Date",
                table: "node_lastvisits",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_node_lastvisits_NodeId",
                table: "node_lastvisits",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_group_id",
                table: "nodes",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_owner_id",
                table: "nodes",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_nodes_RootNodeId",
                table: "nodes",
                column: "RootNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                table: "notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_groups_GroupId",
                table: "questionnaire_groups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_groups_QuestionnaireId",
                table: "questionnaire_groups",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_responses_AnnotationId",
                table: "questionnaire_responses",
                column: "AnnotationId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_responses_NodeId",
                table: "questionnaire_responses",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_responses_OwnerId",
                table: "questionnaire_responses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_responses_QuestionnaireId",
                table: "questionnaire_responses",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaires_OwnerId",
                table: "questionnaires",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaires_QuestionnaireId_IsActive",
                table: "questionnaires",
                columns: new[] { "QuestionnaireId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_questionnaires_QuestionnaireId_Version",
                table: "questionnaires",
                columns: new[] { "QuestionnaireId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_RoleId",
                table: "role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_UserId",
                table: "user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_UserId",
                table: "user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_group_members");

            migrationBuilder.DropTable(
                name: "community_groups");

            migrationBuilder.DropTable(
                name: "eventstore");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "node_lastvisits");

            migrationBuilder.DropTable(
                name: "NodeImports");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "questionnaire_groups");

            migrationBuilder.DropTable(
                name: "questionnaire_responses");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "temp_emails");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "communities");

            migrationBuilder.DropTable(
                name: "annotations");

            migrationBuilder.DropTable(
                name: "questionnaires");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
