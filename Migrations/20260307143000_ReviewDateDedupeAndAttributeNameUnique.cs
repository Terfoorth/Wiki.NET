using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wiki_Blaze.Data;

#nullable disable

namespace Wiki_Blaze.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260307143000_ReviewDateDedupeAndAttributeNameUnique")]
    public partial class ReviewDateDedupeAndAttributeNameUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                SET NOCOUNT ON;

                DECLARE @CanonicalReviewDateId INT;

                SELECT @CanonicalReviewDateId = MIN([Id])
                FROM [WikiAttributeDefinitions]
                WHERE [Name] = N'ReviewDate';

                IF @CanonicalReviewDateId IS NOT NULL
                BEGIN
                    ;WITH [ReviewDefinitionIds] AS (
                        SELECT [Id]
                        FROM [WikiAttributeDefinitions]
                        WHERE [Name] = N'ReviewDate'
                    ),
                    [RankedPageValues] AS (
                        SELECT
                            [value].[Id],
                            ROW_NUMBER() OVER (
                                PARTITION BY [value].[WikiPageId]
                                ORDER BY [value].[Id] DESC
                            ) AS [RowNum]
                        FROM [WikiPageAttributeValues] AS [value]
                        INNER JOIN [ReviewDefinitionIds] AS [definition]
                            ON [definition].[Id] = [value].[AttributeDefinitionId]
                    )
                    DELETE FROM [WikiPageAttributeValues]
                    WHERE [Id] IN (
                        SELECT [Id]
                        FROM [RankedPageValues]
                        WHERE [RowNum] > 1
                    );

                    UPDATE [value]
                    SET [value].[AttributeDefinitionId] = @CanonicalReviewDateId
                    FROM [WikiPageAttributeValues] AS [value]
                    INNER JOIN [WikiAttributeDefinitions] AS [definition]
                        ON [definition].[Id] = [value].[AttributeDefinitionId]
                    WHERE [definition].[Name] = N'ReviewDate'
                      AND [value].[AttributeDefinitionId] <> @CanonicalReviewDateId;

                    ;WITH [ReviewDefinitionIds] AS (
                        SELECT [Id]
                        FROM [WikiAttributeDefinitions]
                        WHERE [Name] = N'ReviewDate'
                    ),
                    [RankedTemplateValues] AS (
                        SELECT
                            [template].[Id],
                            ROW_NUMBER() OVER (
                                PARTITION BY [template].[TemplateId]
                                ORDER BY [template].[Id] DESC
                            ) AS [RowNum]
                        FROM [WikiAttributeTemplateAttributes] AS [template]
                        INNER JOIN [ReviewDefinitionIds] AS [definition]
                            ON [definition].[Id] = [template].[AttributeDefinitionId]
                    )
                    DELETE FROM [WikiAttributeTemplateAttributes]
                    WHERE [Id] IN (
                        SELECT [Id]
                        FROM [RankedTemplateValues]
                        WHERE [RowNum] > 1
                    );

                    UPDATE [template]
                    SET [template].[AttributeDefinitionId] = @CanonicalReviewDateId
                    FROM [WikiAttributeTemplateAttributes] AS [template]
                    INNER JOIN [WikiAttributeDefinitions] AS [definition]
                        ON [definition].[Id] = [template].[AttributeDefinitionId]
                    WHERE [definition].[Name] = N'ReviewDate'
                      AND [template].[AttributeDefinitionId] <> @CanonicalReviewDateId;

                    DELETE FROM [WikiAttributeDefinitions]
                    WHERE [Name] = N'ReviewDate'
                      AND [Id] <> @CanonicalReviewDateId;
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WikiAttributeDefinitions_Name",
                table: "WikiAttributeDefinitions",
                column: "Name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WikiAttributeDefinitions_Name",
                table: "WikiAttributeDefinitions");
        }
    }
}
