using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wiki_Blaze.Migrations
{
    /// <inheritdoc />
    public partial class OnboardingV2StammdatenAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_LinkedUserId",
                table: "OnboardingProfiles");

            migrationBuilder.AddColumn<string>(
                name: "AssignedAgentUserId",
                table: "OnboardingProfiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceNumber",
                table: "OnboardingProfiles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EntryDate",
                table: "OnboardingProfiles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(GETUTCDATE() AS date)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitDate",
                table: "OnboardingProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "OnboardingProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Hostname",
                table: "OnboardingProfiles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "OnboardingProfiles",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "OnboardingProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "OnboardingProfiles",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "OnboardingProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "OnboardingProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Salutation",
                table: "OnboardingProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TicketNumber",
                table: "OnboardingProfiles",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "OnboardingProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE p
                SET p.FirstName = CASE
                        WHEN CHARINDEX(' ', n.TrimmedName) > 0 THEN LEFT(n.TrimmedName, CHARINDEX(' ', n.TrimmedName) - 1)
                        ELSE n.TrimmedName
                    END,
                    p.LastName = CASE
                        WHEN CHARINDEX(' ', n.TrimmedName) > 0 THEN LTRIM(SUBSTRING(n.TrimmedName, CHARINDEX(' ', n.TrimmedName) + 1, LEN(n.TrimmedName)))
                        ELSE N''
                    END
                FROM OnboardingProfiles p
                CROSS APPLY (SELECT LTRIM(RTRIM(ISNULL(p.FullName, N''))) AS TrimmedName) n
                WHERE ISNULL(LTRIM(RTRIM(p.FirstName)), N'') = N''
                  AND ISNULL(LTRIM(RTRIM(p.LastName)), N'') = N''
                  AND n.TrimmedName <> N'';
                """);

            migrationBuilder.Sql(
                """
                UPDATE OnboardingProfiles
                SET Phone = PhoneNumber
                WHERE ISNULL(LTRIM(RTRIM(Phone)), N'') = N''
                  AND ISNULL(LTRIM(RTRIM(PhoneNumber)), N'') <> N'';
                """);

            migrationBuilder.Sql(
                """
                UPDATE OnboardingProfiles
                SET PhoneNumber = Phone
                WHERE ISNULL(LTRIM(RTRIM(PhoneNumber)), N'') = N''
                  AND ISNULL(LTRIM(RTRIM(Phone)), N'') <> N'';
                """);

            migrationBuilder.Sql(
                """
                UPDATE OnboardingProfiles
                SET FullName = LTRIM(RTRIM(CONCAT(ISNULL(FirstName, N''), N' ', ISNULL(LastName, N''))))
                WHERE ISNULL(LTRIM(RTRIM(FullName)), N'') = N''
                  AND (ISNULL(LTRIM(RTRIM(FirstName)), N'') <> N'' OR ISNULL(LTRIM(RTRIM(LastName)), N'') <> N'');
                """);

            migrationBuilder.Sql(
                """
                UPDATE OnboardingProfiles
                SET EntryDate = CAST(COALESCE(StartDate, CreatedAt, GETUTCDATE()) AS date)
                WHERE EntryDate IS NULL OR EntryDate = '0001-01-01T00:00:00.0000000';
                """);

            migrationBuilder.CreateTable(
                name: "OnboardingProfileAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingProfileAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingProfileAttachments_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OnboardingProfileAttachments_OnboardingProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "OnboardingProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfiles_AssignedAgentUserId",
                table: "OnboardingProfiles",
                column: "AssignedAgentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfiles_LastName_FirstName",
                table: "OnboardingProfiles",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfileAttachments_ProfileId",
                table: "OnboardingProfileAttachments",
                column: "ProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfileAttachments_UploadedByUserId",
                table: "OnboardingProfileAttachments",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_AssignedAgentUserId",
                table: "OnboardingProfiles",
                column: "AssignedAgentUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_LinkedUserId",
                table: "OnboardingProfiles",
                column: "LinkedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_AssignedAgentUserId",
                table: "OnboardingProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_LinkedUserId",
                table: "OnboardingProfiles");

            migrationBuilder.DropTable(
                name: "OnboardingProfileAttachments");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProfiles_AssignedAgentUserId",
                table: "OnboardingProfiles");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProfiles_LastName_FirstName",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "AssignedAgentUserId",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "DeviceNumber",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "EntryDate",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "ExitDate",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Hostname",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Salutation",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "TicketNumber",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "OnboardingProfiles");

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_LinkedUserId",
                table: "OnboardingProfiles",
                column: "LinkedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
