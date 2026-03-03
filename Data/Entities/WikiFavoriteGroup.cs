using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiFavoriteGroup
    {
        public const string DefaultGroupName = "Favoriten";

        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = DefaultGroupName;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? SortOrder { get; set; }

        public ApplicationUser? User { get; set; }

        [InverseProperty(nameof(WikiPageFavorite.FavoriteGroup))]
        public ICollection<WikiPageFavorite> Favorites { get; set; } = new List<WikiPageFavorite>();
        
    }
}
