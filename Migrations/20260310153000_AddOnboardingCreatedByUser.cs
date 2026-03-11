using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wiki_Blaze.Data;

#nullable disable

namespace Wiki_Blaze.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260310153000_AddOnboardingCreatedByUser")]
    public partial class AddOnboardingCreatedByUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "OnboardingProfiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProfiles_CreatedByUserId",
                table: "OnboardingProfiles",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_CreatedByUserId",
                table: "OnboardingProfiles",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingProfiles_AspNetUsers_CreatedByUserId",
                table: "OnboardingProfiles");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProfiles_CreatedByUserId",
                table: "OnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "OnboardingProfiles");
        }
    }
}
