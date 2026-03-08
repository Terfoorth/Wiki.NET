using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wiki_Blaze.Data.Entities;

public class WikiEntryViewEvent
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WikiPageId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime ViewedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(WikiPageId))]
    public WikiPage? WikiPage { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

public class WikiTemplateUsageEvent
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WikiPageId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime UsedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(WikiPageId))]
    public WikiPage? WikiPage { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

public class WikiFavoriteUsageEvent
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WikiPageId { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    public DateTime UsedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(WikiPageId))]
    public WikiPage? WikiPage { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
