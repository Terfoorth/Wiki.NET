using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiAttributeTemplateAttribute
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TemplateId { get; set; }

        [Required]
        public int AttributeDefinitionId { get; set; }

        public int SortOrder { get; set; }

        public bool IsRequired { get; set; }

        [ForeignKey("TemplateId")]
        public WikiAttributeTemplate? Template { get; set; }

        [ForeignKey("AttributeDefinitionId")]
        public WikiAttributeDefinition? AttributeDefinition { get; set; }
    }
}
