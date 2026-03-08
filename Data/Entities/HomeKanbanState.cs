using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Services;

namespace Wiki_Blaze.Data.Entities;

public class HomeKanbanColumnState
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public HomeKanbanViewType ViewType { get; set; }

    [Required]
    [MaxLength(80)]
    public string ColumnKey { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

public class HomeKanbanCardState
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public HomeKanbanViewType ViewType { get; set; }

    public HomeKanbanCardEntityType EntityType { get; set; }

    public int EntryId { get; set; }

    [Required]
    [MaxLength(80)]
    public string ColumnKey { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
