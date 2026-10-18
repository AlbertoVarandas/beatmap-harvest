using System.Security.Cryptography;
using System.Text.Json;
using BeatmapHarvest.Core.Json;
using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// Keeps the OAuth credentials encrypted with Windows DPAPI (current user scope):
/// only this Windows account on this machine can decrypt <c>credentials.bin</c>.
/// </summary>
public sealed class DpapiCredentialStore(AppPaths paths) : ICredentialStore
{
    private static readonly byte[] Entropy = "BeatmapHarvest.credentials.v1"u8.ToArray();

    public OsuCredentials? Load()
    {
        if (!File.Exists(paths.CredentialsFile))
            return null;
        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(paths.CredentialsFile), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<OsuCredentials>(bytes, JsonDefaults.Files);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // Copied from another account/machine, or damaged: ask for the credentials again.
            return null;
        }
    }

    public void Save(OsuCredentials credentials)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(credentials, JsonDefaults.Files);
        AtomicFile.WriteAllBytes(paths.CredentialsFile, ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser));
    }

    public void Clear()
    {
        if (File.Exists(paths.CredentialsFile))
            File.Delete(paths.CredentialsFile);
    }
}
