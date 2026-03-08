using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Services;

namespace Wiki_Blaze.Data.Entities;

public class HomeEntryComment
{
    [Key]
    public int Id { get; set; }

    public HomeCommentScope Scope { get; set; }

    public int EntryId { get; set; }

    [MaxLength(450)]
    public string? AuthorId { get; set; }

    [Required]
    [StringLength(4000)]
    public string Text { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? MentionTokensJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AuthorId))]
    public ApplicationUser? Author { get; set; }

    public ICollection<HomeEntryCommentAttachment> Attachments { get; set; } = new List<HomeEntryCommentAttachment>();
}

public class HomeEntryCommentAttachment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CommentId { get; set; }

    [Required]
    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CommentId))]
    public HomeEntryComment? Comment { get; set; }
}
