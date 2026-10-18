using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// Whether the app is set up (osu! API credentials saved). Until it is, only Settings is available;
/// the sidebar listens to <see cref="Changed"/> to enable the other tabs.
/// </summary>
public sealed class SetupState(ICredentialStore credentials)
{
    private bool? _hasCredentials;

    public event Action? Changed;

    public bool HasCredentials => _hasCredentials ??= credentials.Load() is not null;

    /// <summary>Call after the credentials are saved or removed.</summary>
    public void Refresh()
    {
        _hasCredentials = credentials.Load() is not null;
        Changed?.Invoke();
    }
}
