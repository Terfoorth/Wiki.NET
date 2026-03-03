using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiComment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WikiPageId { get; set; }

        [MaxLength(450)]
        public string? AuthorId { get; set; }

        [Required]
        [StringLength(2000)]
        public string Text { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("WikiPageId")]
        public WikiPage? WikiPage { get; set; }

        [ForeignKey("AuthorId")]
        public ApplicationUser? Author { get; set; }
    }
}
