using System.Text.Json;
using Microsoft.JSInterop;

namespace Sora.Blazor.Services;

public sealed class UiPreferencesService(IJSRuntime js)
{
    private const string StoragePrefix = "sora.ui.preferences.v1.";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public UiPreferences Current { get; private set; } = new();
    public string? CurrentUserKey { get; private set; }

    public async Task LoadAsync(string userKey)
    {
        if (string.Equals(CurrentUserKey, userKey, StringComparison.Ordinal)) return;
        CurrentUserKey = userKey;
        var json = await js.InvokeAsync<string?>("soraUiPreferences.get", StoragePrefix + userKey);
        Current = Parse(json);
        await ApplyAsync();
    }

    public async Task SaveAsync(string userKey, UiPreferences preferences)
    {
        CurrentUserKey = userKey;
        Current = Normalize(preferences);
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        await js.InvokeVoidAsync("soraUiPreferences.set", StoragePrefix + userKey, json);
        await ApplyAsync();
    }

    public async Task ResetViewAsync()
    {
        CurrentUserKey = null;
        Current = new();
        await ApplyAsync();
    }

    private Task ApplyAsync() => js.InvokeVoidAsync("soraUiPreferences.apply", JsonSerializer.Serialize(Current, JsonOptions)).AsTask();

    private static UiPreferences Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return Normalize(JsonSerializer.Deserialize<UiPreferences>(json, JsonOptions) ?? new()); }
        catch (JsonException) { return new(); }
    }

    private static UiPreferences Normalize(UiPreferences value) => new()
    {
        Theme = value.Theme is "light" or "dark" or "system" ? value.Theme : "light",
        FontScale = value.FontScale is 100 or 112 or 125 or 150 or 200 ? value.FontScale : 100,
        HighContrast = value.HighContrast
    };
}

public sealed class UiPreferences
{
    public string Theme { get; set; } = "light";
    public int FontScale { get; set; } = 100;
    public bool HighContrast { get; set; }
}
