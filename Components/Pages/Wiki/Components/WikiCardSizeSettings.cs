namespace Wiki_Blaze.Components.Pages.Wiki.Components;

public sealed class WikiCardSizeSettings
{
    public const int Min = 320;
    public const int Max = 700;
    public const int DefaultWidth = 420;
    public const int DefaultHeight = 420;
    public const int LegacyCompactWidth = 340;
    public const int LegacyCompactHeight = 320;

    public int CardWidthPx { get; set; } = DefaultWidth;
    public int CardHeightPx { get; set; } = DefaultHeight;

    public void Normalize()
    {
        CardWidthPx = Clamp(CardWidthPx);
        CardHeightPx = Clamp(CardHeightPx);
    }

    public static int Clamp(int value) => Math.Clamp(value, Min, Max);
}
