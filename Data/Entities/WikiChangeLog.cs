using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiChangeLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WikiPageId { get; set; }

        [MaxLength(450)]
        public string? AuthorId { get; set; }

        public WikiChangeType ChangeType { get; set; } = WikiChangeType.Updated;

        [Required]
        [StringLength(500)]
        public string Summary { get; set; } = string.Empty;

        public string? OptionalDiff { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("WikiPageId")]
        public WikiPage? WikiPage { get; set; }

        [ForeignKey("AuthorId")]
        public ApplicationUser? Author { get; set; }
    }

    public enum WikiChangeType
    {
        Created = 0,
        Updated = 1,
        Commented = 2,
        Assigned = 3
    }
}
