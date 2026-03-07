using System.ComponentModel.DataAnnotations;

namespace Wiki_Blaze.Services;

public class ReminderEmailOptions
{
    public const string SectionName = "ReminderEmail";

    public bool Enabled { get; set; }

    [Range(0, 23)]
    public int SendHourLocal { get; set; } = 8;

    [Required]
    [MaxLength(300)]
    public string BaseUrl { get; set; } = string.Empty;
}
