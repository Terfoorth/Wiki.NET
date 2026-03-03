using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiPageAttributeValue
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WikiPageId { get; set; }

        [Required]
        public int AttributeDefinitionId { get; set; }

        [MaxLength(500)]
        public string? Value { get; set; }

        [ForeignKey("WikiPageId")]
        public WikiPage? WikiPage { get; set; }

        [ForeignKey("AttributeDefinitionId")]
        public WikiAttributeDefinition? AttributeDefinition { get; set; }
    }
}
