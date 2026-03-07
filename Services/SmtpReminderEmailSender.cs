using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Wiki_Blaze.Services;

public class SmtpReminderEmailSender(IOptionsMonitor<ReminderSmtpOptions> smtpOptionsMonitor) : IReminderEmailSender
{
    public async Task SendAsync(ReminderEmailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var smtpOptions = smtpOptionsMonitor.CurrentValue;
        ValidateSettings(smtpOptions);

        using var mail = new MailMessage
        {
            From = CreateFromAddress(smtpOptions),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };

        mail.To.Add(new MailAddress(message.ToAddress));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));

        using var client = new SmtpClient(smtpOptions.Host, smtpOptions.Port)
        {
            EnableSsl = smtpOptions.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(smtpOptions.Username))
        {
            client.Credentials = new NetworkCredential(smtpOptions.Username, smtpOptions.Password);
        }

        await client.SendMailAsync(mail);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static MailAddress CreateFromAddress(ReminderSmtpOptions smtpOptions)
    {
        return string.IsNullOrWhiteSpace(smtpOptions.FromName)
            ? new MailAddress(smtpOptions.FromAddress)
            : new MailAddress(smtpOptions.FromAddress, smtpOptions.FromName);
    }

    private static void ValidateSettings(ReminderSmtpOptions smtpOptions)
    {
        if (string.IsNullOrWhiteSpace(smtpOptions.Host))
        {
            throw new InvalidOperationException("Reminder SMTP host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(smtpOptions.FromAddress))
        {
            throw new InvalidOperationException("Reminder SMTP from address is not configured.");
        }

        if (smtpOptions.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Reminder SMTP port must be between 1 and 65535.");
        }
    }
}
