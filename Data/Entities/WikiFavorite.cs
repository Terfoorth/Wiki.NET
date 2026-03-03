using System.ComponentModel.DataAnnotations;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiFavorite
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int WikiPageId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public WikiPage? WikiPage { get; set; }
    }
}
