using System.Text.Json;
using Microsoft.JSInterop;

namespace Wiki_Blaze.Components.Pages.Wiki.Components;

public static class WikiCardSizeSettingsStore
{
    public const string StorageKey = "wikiBlaze.cardViewSize.v1";

    public static async Task<WikiCardSizeSettings?> TryLoadAsync(IJSRuntime jsRuntime)
    {
        try
        {
            var payload = await jsRuntime.InvokeAsync<string?>("wikiBlaze.loadDashboardSettings", StorageKey);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<WikiCardSizeSettings>(payload);
            if (settings is null)
            {
                return null;
            }

            settings.Normalize();
            return settings;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<WikiCardSizeSettings> LoadOrDefaultAsync(IJSRuntime jsRuntime)
    {
        return await TryLoadAsync(jsRuntime) ?? new WikiCardSizeSettings();
    }

    public static async Task SaveAsync(IJSRuntime jsRuntime, WikiCardSizeSettings settings)
    {
        settings.Normalize();
        var payload = JsonSerializer.Serialize(settings);
        await jsRuntime.InvokeVoidAsync("wikiBlaze.saveDashboardSettings", StorageKey, payload);
    }
}
