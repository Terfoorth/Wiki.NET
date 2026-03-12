using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wiki_Blaze.Migrations
{
    /// <inheritdoc />
    public partial class MigrateWikiCommentsToHomeEntryComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
INSERT INTO [HomeEntryComments] ([Scope], [EntryId], [AuthorId], [Text], [MentionTokensJson], [CreatedAtUtc])
SELECT
    0 AS [Scope],                -- HomeCommentScope.Wiki
    [wc].[WikiPageId] AS [EntryId],
    [wc].[AuthorId],
    [wc].[Text],
    NULL AS [MentionTokensJson], -- legacy model had no mention tokens
    [wc].[CreatedAt] AS [CreatedAtUtc]
FROM [WikiComments] AS [wc]
WHERE NOT EXISTS
(
    SELECT 1
    FROM [HomeEntryComments] AS [hec]
    WHERE [hec].[Scope] = 0
      AND [hec].[EntryId] = [wc].[WikiPageId]
      AND [hec].[Text] = [wc].[Text]
      AND [hec].[CreatedAtUtc] = [wc].[CreatedAt]
      AND
      (
          ([hec].[AuthorId] = [wc].[AuthorId])
          OR ([hec].[AuthorId] IS NULL AND [wc].[AuthorId] IS NULL)
      )
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op to avoid removing comments created or edited after migration.
        }
    }
}
