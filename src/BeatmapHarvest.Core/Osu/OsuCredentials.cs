namespace BeatmapHarvest.Core.Osu;

/// <summary>
/// The user's own OAuth application (osu.ppy.sh/home/account/edit → OAuth).
/// Every user of the app registers their own; the secret never ships with the app.
/// </summary>
public sealed record OsuCredentials(int ClientId, string ClientSecret)
{
    // Records print every property by default; never let the secret reach logs or the UI.
    public override string ToString() => $"OsuCredentials {{ ClientId = {ClientId} }}";
}

/// <summary>Persists the credentials. The app implements this with OS-level encryption.</summary>
public interface ICredentialStore
{
    OsuCredentials? Load();
    void Save(OsuCredentials credentials);
    void Clear();
}
