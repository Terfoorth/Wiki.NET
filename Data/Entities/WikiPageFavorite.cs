using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiPageFavorite
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int WikiPageId { get; set; }

        [Required]
        public int FavoriteGroupId { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("WikiPageId")]
        public WikiPage? WikiPage { get; set; }

        [ForeignKey("FavoriteGroupId")]
        public WikiFavoriteGroup? FavoriteGroup { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
    }
}
