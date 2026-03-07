using System.ComponentModel.DataAnnotations;

namespace Wiki_Blaze.Services;

public class ReminderSmtpOptions
{
    public const string SectionName = "ReminderSmtp";

    [Required]
    [MaxLength(200)]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    [MaxLength(200)]
    public string? Username { get; set; }

    [MaxLength(400)]
    public string? Password { get; set; }

    [Required]
    [MaxLength(254)]
    public string FromAddress { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FromName { get; set; }
}
