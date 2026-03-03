using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wiki_Blaze.Data.Entities
{
    public class WikiCategory
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Der Name der Kategorie ist erforderlich.")]
        [StringLength(100, ErrorMessage = "Der Name darf maximal 100 Zeichen lang sein.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Die Beschreibung darf maximal 500 Zeichen lang sein.")]
        public string? Description { get; set; }

        public bool IsFormCategory { get; set; }

        // Für die hierarchische Struktur (DxTreeList)
        public int? ParentId { get; set; }

        [ForeignKey("ParentId")]
        public virtual WikiCategory? Parent { get; set; }

        public virtual ICollection<WikiCategory> Children { get; set; } = new List<WikiCategory>();

        // Eine Kategorie kann viele Wiki-Seiten haben
        public virtual ICollection<WikiPage> Pages { get; set; } = new List<WikiPage>();
    }
}