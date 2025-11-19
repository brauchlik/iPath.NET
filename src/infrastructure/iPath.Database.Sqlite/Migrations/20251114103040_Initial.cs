using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace iPath.EF.Sqlite.Migrations
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
                    event_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    event_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    event_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    object_name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    object_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventstore", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "node_imports",
                columns: table => new
                {
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: true),
                    info = table.Column<string>(type: "TEXT", nullable: true),
                    id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_node_imports", x => x.node_id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    ipath2_password = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", nullable: true),
                    security_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    phone_number = table.Column<string>(type: "TEXT", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    access_failed_count = table.Column<int>(type: "INTEGER", nullable: false),
                    profile = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "communities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    base_url = table.Column<string>(type: "TEXT", nullable: true),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    deleted_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified_on = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_communities", x => x.id);
                    table.ForeignKey(
                        name: "fk_communities_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    storage_id = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    group_type = table.Column<int>(type: "INTEGER", nullable: false),
                    visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    deleted_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    settings = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_groups_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "questionnaires",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    questionnaire_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    resource = table.Column<string>(type: "TEXT", nullable: false),
                    deleted_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified_on = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questionnaires", x => x.id);
                    table.ForeignKey(
                        name: "fk_questionnaires_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "TEXT", nullable: false),
                    provider_key = table.Column<string>(type: "TEXT", nullable: false),
                    provider_display_name = table.Column<string>(type: "TEXT", nullable: true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    login_provider = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_member",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    community_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<int>(type: "INTEGER", nullable: false),
                    all_groups = table.Column<bool>(type: "INTEGER", nullable: false),
                    i_path2_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_member", x => x.id);
                    table.ForeignKey(
                        name: "fk_community_member_communities_community_id",
                        column: x => x.community_id,
                        principalTable: "communities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_community_member_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_group",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    community_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_group", x => x.id);
                    table.ForeignKey(
                        name: "fk_community_group_communities_community_id",
                        column: x => x.community_id,
                        principalTable: "communities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_community_group_groups_group_id",
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
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<int>(type: "INTEGER", nullable: false),
                    is_favourite = table.Column<bool>(type: "INTEGER", nullable: false),
                    notifications = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ipath2_id = table.Column<int>(type: "INTEGER", nullable: true),
                    storage_id = table.Column<string>(type: "TEXT", nullable: true),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    root_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    parent_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    sort_nr = table.Column<int>(type: "INTEGER", nullable: true),
                    node_type = table.Column<string>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    file = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_nodes_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_nodes_nodes_root_node_id",
                        column: x => x.root_node_id,
                        principalTable: "nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_nodes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_nodes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    questionnaire_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    group_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    usage = table.Column<int>(type: "INTEGER", nullable: false),
                    explicit_version = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questionnaire_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_questionnaire_groups_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_questionnaire_groups_questionnaires_questionnaire_id",
                        column: x => x.questionnaire_id,
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
                    node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    text = table.Column<string>(type: "TEXT", nullable: true),
                    deleted_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified_on = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_annotations", x => x.id);
                    table.ForeignKey(
                        name: "fk_annotations_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_annotations_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "node_lastvisits",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_node_lastvisits", x => new { x.user_id, x.node_id });
                    table.ForeignKey(
                        name: "fk_node_lastvisits_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_node_lastvisits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_responses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    questionnaire_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    modified_on = table.Column<DateTime>(type: "TEXT", nullable: false),
                    node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    annotation_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    resource = table.Column<string>(type: "jsonb", nullable: false),
                    annotation_id1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    node_id1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    deleted_on = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified_on = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questionnaire_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_questionnaire_responses_annotations_annotation_id",
                        column: x => x.annotation_id,
                        principalTable: "annotations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_questionnaire_responses_annotations_annotation_id1",
                        column: x => x.annotation_id1,
                        principalTable: "annotations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_questionnaire_responses_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_questionnaire_responses_nodes_node_id1",
                        column: x => x.node_id1,
                        principalTable: "nodes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_questionnaire_responses_questionnaires_questionnaire_id",
                        column: x => x.questionnaire_id,
                        principalTable: "questionnaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_questionnaire_responses_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_annotations_node_id",
                table: "annotations",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_annotations_owner_id",
                table: "annotations",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_communities_owner_id",
                table: "communities",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_group_community_id",
                table: "community_group",
                column: "community_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_group_group_id_community_id",
                table: "community_group",
                columns: new[] { "group_id", "community_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_member_community_id",
                table: "community_member",
                column: "community_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_member_user_id_community_id",
                table: "community_member",
                columns: new[] { "user_id", "community_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventstore_event_date",
                table: "eventstore",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "ix_eventstore_event_name",
                table: "eventstore",
                column: "event_name");

            migrationBuilder.CreateIndex(
                name: "ix_eventstore_object_id",
                table: "eventstore",
                column: "object_id");

            migrationBuilder.CreateIndex(
                name: "ix_eventstore_object_name",
                table: "eventstore",
                column: "object_name");

            migrationBuilder.CreateIndex(
                name: "ix_group_members_group_id",
                table: "group_members",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_members_user_id_group_id",
                table: "group_members",
                columns: new[] { "user_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_groups_owner_id",
                table: "groups",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_node_lastvisits_date",
                table: "node_lastvisits",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_node_lastvisits_node_id",
                table: "node_lastvisits",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_group_id",
                table: "nodes",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_owner_id",
                table: "nodes",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_root_node_id",
                table: "nodes",
                column: "root_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_user_id",
                table: "nodes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_groups_group_id",
                table: "questionnaire_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_groups_questionnaire_id",
                table: "questionnaire_groups",
                column: "questionnaire_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_responses_annotation_id",
                table: "questionnaire_responses",
                column: "annotation_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_responses_annotation_id1",
                table: "questionnaire_responses",
                column: "annotation_id1");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_responses_node_id",
                table: "questionnaire_responses",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_responses_node_id1",
                table: "questionnaire_responses",
                column: "node_id1");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_responses_owner_id",
                table: "questionnaire_responses",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaire_responses_questionnaire_id",
                table: "questionnaire_responses",
                column: "questionnaire_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaires_owner_id",
                table: "questionnaires",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_questionnaires_questionnaire_id_is_active",
                table: "questionnaires",
                columns: new[] { "questionnaire_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_questionnaires_questionnaire_id_version",
                table: "questionnaires",
                columns: new[] { "questionnaire_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_group");

            migrationBuilder.DropTable(
                name: "community_member");

            migrationBuilder.DropTable(
                name: "eventstore");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "node_imports");

            migrationBuilder.DropTable(
                name: "node_lastvisits");

            migrationBuilder.DropTable(
                name: "questionnaire_groups");

            migrationBuilder.DropTable(
                name: "questionnaire_responses");

            migrationBuilder.DropTable(
                name: "role_claims");

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
