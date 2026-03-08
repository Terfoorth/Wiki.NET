using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wiki_Blaze.Migrations
{
    /// <inheritdoc />
    public partial class HomeKanbanV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [OnboardingProfiles]
SET [Status] = CASE [Status]
    WHEN 1 THEN 2
    WHEN 2 THEN 3
    WHEN 3 THEN 4
    ELSE [Status]
END;");

            migrationBuilder.CreateTable(
                name: "HomeEntryComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    EntryId = table.Column<int>(type: "int", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MentionTokensJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeEntryComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeEntryComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HomeKanbanCardStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ViewType = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntryId = table.Column<int>(type: "int", nullable: false),
                    ColumnKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeKanbanCardStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeKanbanCardStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeKanbanColumnStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ViewType = table.Column<int>(type: "int", nullable: false),
                    ColumnKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeKanbanColumnStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeKanbanColumnStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WikiEntryViewEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WikiPageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ViewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiEntryViewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiEntryViewEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WikiEntryViewEvents_WikiPages_WikiPageId",
                        column: x => x.WikiPageId,
                        principalTable: "WikiPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WikiFavoriteUsageEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WikiPageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiFavoriteUsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiFavoriteUsageEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WikiFavoriteUsageEvents_WikiPages_WikiPageId",
                        column: x => x.WikiPageId,
                        principalTable: "WikiPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WikiTemplateUsageEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WikiPageId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiTemplateUsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiTemplateUsageEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WikiTemplateUsageEvents_WikiPages_WikiPageId",
                        column: x => x.WikiPageId,
                        principalTable: "WikiPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HomeEntryCommentAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeEntryCommentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HomeEntryCommentAttachments_HomeEntryComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "HomeEntryComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HomeEntryCommentAttachments_CommentId",
                table: "HomeEntryCommentAttachments",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeEntryComments_AuthorId",
                table: "HomeEntryComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_HomeEntryComments_Scope_EntryId_CreatedAtUtc",
                table: "HomeEntryComments",
                columns: new[] { "Scope", "EntryId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeKanbanCardStates_UserId_ViewType_ColumnKey_SortOrder",
                table: "HomeKanbanCardStates",
                columns: new[] { "UserId", "ViewType", "ColumnKey", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeKanbanCardStates_UserId_ViewType_EntityType_EntryId",
                table: "HomeKanbanCardStates",
                columns: new[] { "UserId", "ViewType", "EntityType", "EntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeKanbanColumnStates_UserId_ViewType_ColumnKey",
                table: "HomeKanbanColumnStates",
                columns: new[] { "UserId", "ViewType", "ColumnKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeKanbanColumnStates_UserId_ViewType_SortOrder",
                table: "HomeKanbanColumnStates",
                columns: new[] { "UserId", "ViewType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WikiEntryViewEvents_UserId_WikiPageId_ViewedAtUtc",
                table: "WikiEntryViewEvents",
                columns: new[] { "UserId", "WikiPageId", "ViewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WikiEntryViewEvents_WikiPageId",
                table: "WikiEntryViewEvents",
                column: "WikiPageId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiFavoriteUsageEvents_UserId_WikiPageId_UsedAtUtc",
                table: "WikiFavoriteUsageEvents",
                columns: new[] { "UserId", "WikiPageId", "UsedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WikiFavoriteUsageEvents_WikiPageId",
                table: "WikiFavoriteUsageEvents",
                column: "WikiPageId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiTemplateUsageEvents_UserId_WikiPageId_UsedAtUtc",
                table: "WikiTemplateUsageEvents",
                columns: new[] { "UserId", "WikiPageId", "UsedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WikiTemplateUsageEvents_WikiPageId",
                table: "WikiTemplateUsageEvents",
                column: "WikiPageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeEntryCommentAttachments");

            migrationBuilder.DropTable(
                name: "HomeKanbanCardStates");

            migrationBuilder.DropTable(
                name: "HomeKanbanColumnStates");

            migrationBuilder.DropTable(
                name: "WikiEntryViewEvents");

            migrationBuilder.DropTable(
                name: "WikiFavoriteUsageEvents");

            migrationBuilder.DropTable(
                name: "WikiTemplateUsageEvents");

            migrationBuilder.DropTable(
                name: "HomeEntryComments");

            migrationBuilder.Sql(@"
UPDATE [OnboardingProfiles]
SET [Status] = CASE [Status]
    WHEN 2 THEN 1
    WHEN 3 THEN 2
    WHEN 4 THEN 3
    ELSE [Status]
END;");
        }
    }
}
