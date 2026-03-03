using System.ComponentModel.DataAnnotations;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiAttributeTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }
    }
}
