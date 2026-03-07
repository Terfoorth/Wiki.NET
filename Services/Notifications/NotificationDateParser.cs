using System.Globalization;

namespace Wiki_Blaze.Services.Notifications;

internal static class NotificationDateParser
{
    private static readonly string[] SupportedFormats =
    [
        "yyyy-MM-dd",
        "dd.MM.yyyy",
        "d.M.yyyy",
        "yyyy/MM/dd",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssK"
    ];

    private static readonly CultureInfo[] SupportedCultures =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("de-DE"),
        CultureInfo.GetCultureInfo("en-US")
    ];

    public static bool TryParseToDate(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        foreach (var culture in SupportedCultures)
        {
            if (DateTime.TryParseExact(trimmed, SupportedFormats, culture, DateTimeStyles.AllowWhiteSpaces, out var parsedExact))
            {
                date = parsedExact.Date;
                return true;
            }

            if (DateTime.TryParse(trimmed, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                date = parsed.Date;
                return true;
            }
        }

        return false;
    }
}
