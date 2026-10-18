using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BeatmapHarvest.Core.Json;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// A JSON file encrypted with Windows DPAPI (current user scope): only this Windows account on this machine can read it.
/// Used for the OAuth client secret and the signed-in user's tokens.
/// </summary>
internal static class DpapiFile
{
    public static T? Read<T>(string path, string purpose) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy(purpose), DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<T>(bytes, JsonDefaults.Files);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // Copied from another account/machine, or damaged: treat as missing.
            return null;
        }
    }

    public static void Write<T>(string path, string purpose, T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Files);
        AtomicFile.WriteAllBytes(path, ProtectedData.Protect(bytes, Entropy(purpose), DataProtectionScope.CurrentUser));
    }

    public static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static byte[] Entropy(string purpose) => Encoding.UTF8.GetBytes($"BeatmapHarvest.{purpose}.v1");
}
