using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Wiki_Blaze.Data;

namespace Wiki_Blaze.Data.Entities;

public class OnboardingProfileAttachment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProfileId { get; set; }

    [Required]
    [MaxLength(260)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    [Required]
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? UploadedByUserId { get; set; }

    [ForeignKey(nameof(ProfileId))]
    public OnboardingProfile? Profile { get; set; }

    [ForeignKey(nameof(UploadedByUserId))]
    public ApplicationUser? UploadedByUser { get; set; }
}
