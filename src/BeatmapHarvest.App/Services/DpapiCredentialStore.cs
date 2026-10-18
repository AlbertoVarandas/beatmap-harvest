using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>Keeps the OAuth client id/secret in <c>credentials.bin</c>, encrypted for the current Windows user.</summary>
public sealed class DpapiCredentialStore(AppPaths paths) : ICredentialStore
{
    private const string Purpose = "credentials";

    public OsuCredentials? Load() => DpapiFile.Read<OsuCredentials>(paths.CredentialsFile, Purpose);

    public void Save(OsuCredentials credentials) => DpapiFile.Write(paths.CredentialsFile, Purpose, credentials);

    public void Clear() => DpapiFile.Delete(paths.CredentialsFile);
}
