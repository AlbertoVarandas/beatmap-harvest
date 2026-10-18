using System.Globalization;
using BeatmapHarvest.Core.Storage;
using Microsoft.AspNetCore.Components;

namespace BeatmapHarvest.App.Services;

public enum AppLanguage
{
    English,
    Portuguese,
}

/// <summary>
/// UI text in English or Portuguese (Portugal). The English text is the key, so a missing translation shows the
/// English text rather than nothing. Changing the language also changes the culture used for dates and numbers.
/// </summary>
public sealed class Localizer(SettingsStore settings)
{
    private AppLanguage? _language;

    public event Action? Changed;

    /// <summary>The chosen language, or the Windows display language when none was chosen yet.</summary>
    public AppLanguage Language => _language ??=
        Enum.TryParse<AppLanguage>(settings.Load().LanguageName, out var saved)
            ? saved
            : CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "pt" ? AppLanguage.Portuguese : AppLanguage.English;

    public CultureInfo Culture => Language == AppLanguage.Portuguese ? CultureInfo.GetCultureInfo("pt-PT") : CultureInfo.GetCultureInfo("en-GB");

    public string this[string english] =>
        Language == AppLanguage.Portuguese && Translations.Portuguese.TryGetValue(english, out var text) ? text : english;

    /// <summary>A translated format string filled in with the current culture, e.g. <c>F("{0} maps", 12)</c>.</summary>
    public string F(string english, params object?[] args) => string.Format(Culture, this[english], args);

    /// <summary>A translated text containing simple markup (<c>&lt;b&gt;</c>, <c>&lt;code&gt;</c>, links). Only for the app's own texts.</summary>
    public MarkupString Html(string english, params object?[] args) =>
        new(args.Length == 0 ? this[english] : string.Format(Culture, this[english], args));

    public void Set(AppLanguage language)
    {
        settings.Save(settings.Load() with { LanguageName = language.ToString() });
        _language = language;
        ApplyCulture();
        Changed?.Invoke();
    }

    /// <summary>
    /// The culture of the chosen language, for formatting numbers and dates. Use this rather than
    /// CultureInfo.CurrentCulture: that one flows with async calls, so a change made in one event handler
    /// does not reach every render.
    /// </summary>
    public static CultureInfo ActiveCulture { get; private set; } = CultureInfo.CurrentCulture;

    /// <summary>Makes dates and numbers follow the language. Call on the UI thread.</summary>
    public void ApplyCulture()
    {
        var culture = Culture;
        ActiveCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
