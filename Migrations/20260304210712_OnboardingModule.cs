using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Wiki_Blaze.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OnboardingChecklistCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingChecklistCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingMeasureCatalogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMeasureCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Supervisor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PrinterCardNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LinkedUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingProfiles_AspNetUsers_LinkedUserId",
                        column: x => x.LinkedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingChecklistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    CatalogItemId = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingChecklistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingChecklistEntries_OnboardingChecklistCatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "OnboardingChecklistCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingChecklistEntries_OnboardingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "OnboardingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingMeasureEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    CatalogItemId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingMeasureEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingMeasureEntries_OnboardingMeasureCatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "OnboardingMeasureCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingMeasureEntries_OnboardingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "OnboardingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "OnboardingChecklistCatalogItems",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Outlook Anmeldung und Mailversand testen", true, "Outlook", 1 },
                    { 2, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Teams Login und Telefonie testen", true, "Teams", 2 },
                    { 3, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Druckerkarte/FollowMe am Gerät testen", true, "Druckerkarte", 3 }
                });

            migrationBuilder.InsertData(
                table: "OnboardingMeasureCatalogItems",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Freigaben und Laufwerkszuordnungen", true, "Shared-Drives", 1 },
                    { 2, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Mailboxen und Berechtigungen", true, "E-Mail Postfächer", 2 },
                    { 3, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Mailverteiler und Teams-Gruppen", true, "Verteilerlisten", 3 },
                    { 4, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Fachapplikationen und Lizenzen", true, "Anwendungen", 4 },
                    { 5, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Rollen und Rechte", true, "Berechtigungen", 5 },
                    { 6, new DateTime(2026, 3, 4, 0, 0, 0, 0, DateTimeKind.Utc), "Weitere individuelle Maßnahmen", true, "Sonstiges", 6 }
                });

            migrationBuilder.InsertData(
                table: "WikiAttributeDefinitions",
                columns: new[] { "Id", "Description", "IsAutoGenerated", "IsRequired", "Name", "ValueType" },
                values: new object[] { 1, "Verantwortliche Person", false, true, "Owner", "text" });

            migrationBuilder.UpdateData(
                table: "WikiAttributeTemplateAttributes",
                keyColumn: "Id",
                keyValue: 2,
                column: "SortOrder",
                value: 2);

            migrationBuilder.UpdateData(
                table: "WikiAttributeTemplateAttributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 3);

            migrationBuilder.InsertData(
                table: "WikiAttributeTemplateAttributes",
                columns: new[] { "Id", "AttributeDefinitionId", "IsRequired", "SortOrder", "TemplateId" },
                values: new object[] { 1, 1, true, 1, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingChecklistCatalogItems_Name",
                table: "OnboardingChecklistCatalogItems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingChecklistEntries_CatalogItemId",
                table: "OnboardingChecklistEntries",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingChecklistEntries_ProfileId_CatalogItemId",
                table: "OnboardingChecklistEntries",
                columns: new[] { "ProfileId", "CatalogItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMeasureCatalogItems_Name",
                table: "OnboardingMeasureCatalogItems",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMeasureEntries_CatalogItemId",
                table: "OnboardingMeasureEntries",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingMeasureEntries_ProfileId_CatalogItemId",
                table: "OnboardingMeasureEntries",
                columns: new[] { "ProfileId", "CatalogItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfiles_FullName_Department",
                table: "OnboardingProfiles",
                columns: new[] { "FullName", "Department" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfiles_LinkedUserId",
                table: "OnboardingProfiles",
                column: "LinkedUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingChecklistEntries");

            migrationBuilder.DropTable(
                name: "OnboardingMeasureEntries");

            migrationBuilder.DropTable(
                name: "OnboardingChecklistCatalogItems");

            migrationBuilder.DropTable(
                name: "OnboardingMeasureCatalogItems");

            migrationBuilder.DropTable(
                name: "OnboardingProfiles");

            migrationBuilder.DeleteData(
                table: "WikiAttributeTemplateAttributes",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "WikiAttributeDefinitions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.UpdateData(
                table: "WikiAttributeTemplateAttributes",
                keyColumn: "Id",
                keyValue: 2,
                column: "SortOrder",
                value: 1);

            migrationBuilder.UpdateData(
                table: "WikiAttributeTemplateAttributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "SortOrder",
                value: 2);
        }
    }
}
