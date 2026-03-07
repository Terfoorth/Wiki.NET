namespace Wiki_Blaze.Services;

public sealed record ReminderEmailMessage(
    string ToAddress,
    string Subject,
    string HtmlBody,
    string TextBody);
