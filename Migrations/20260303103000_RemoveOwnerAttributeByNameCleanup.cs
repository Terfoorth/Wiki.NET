using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wiki_Blaze.Migrations
{
    public partial class RemoveOwnerAttributeByNameCleanup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @OwnerDefinitionIds TABLE ([Id] INT PRIMARY KEY);

INSERT INTO @OwnerDefinitionIds ([Id])
SELECT [Id]
FROM [WikiAttributeDefinitions]
WHERE [Name] = N'Owner';

DELETE pav
FROM [WikiPageAttributeValues] pav
INNER JOIN @OwnerDefinitionIds ownerIds ON ownerIds.[Id] = pav.[AttributeDefinitionId];

DELETE tpa
FROM [WikiAttributeTemplateAttributes] tpa
INNER JOIN @OwnerDefinitionIds ownerIds ON ownerIds.[Id] = tpa.[AttributeDefinitionId];

DELETE ad
FROM [WikiAttributeDefinitions] ad
INNER JOIN @OwnerDefinitionIds ownerIds ON ownerIds.[Id] = ad.[Id];
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
