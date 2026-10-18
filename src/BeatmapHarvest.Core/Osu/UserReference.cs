using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace BeatmapHarvest.Core.Osu;

/// <summary>
/// A profile as typed by the user: a profile URL, a numeric id or a username.
/// <see cref="ApiKey"/> is what goes into <c>/users/{user}</c> (usernames are prefixed with <c>@</c>).
/// </summary>
public sealed partial record UserReference(string ApiKey, string? Mode)
{
    public static bool TryParse(string? input, [NotNullWhen(true)] out UserReference? reference)
    {
        reference = null;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        var url = ProfileUrl().Match(text);
        if (url.Success)
        {
            var user = Uri.UnescapeDataString(url.Groups["user"].Value);
            var mode = url.Groups["mode"].Success ? url.Groups["mode"].Value.ToLowerInvariant() : null;
            if (!Ruleset.IsValid(mode))
                mode = null;
            return TryFromIdOrName(user, mode, out reference);
        }

        // Anything else that looks like a URL is not a profile.
        if (text.Contains("://", StringComparison.Ordinal) || text.Contains('/', StringComparison.Ordinal))
            return false;

        return TryFromIdOrName(text.TrimStart('@'), null, out reference);
    }

    private static bool TryFromIdOrName(string value, string? mode, [NotNullWhen(true)] out UserReference? reference)
    {
        reference = null;
        if (long.TryParse(value, out var id) && id > 0)
        {
            reference = new UserReference(id.ToString(System.Globalization.CultureInfo.InvariantCulture), mode);
            return true;
        }

        if (!Username().IsMatch(value))
            return false;

        reference = new UserReference("@" + value, mode);
        return true;
    }

    // https://osu.ppy.sh/users/123, /users/123/mania, /u/name
    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?osu\.ppy\.sh/(?:users|u)/(?<user>[^/?#]+)(?:/(?<mode>[a-z]+))?/?(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileUrl();

    // osu! usernames: letters, digits, spaces, '-', '_', '[' and ']', up to 15 characters.
    [GeneratedRegex(@"^[A-Za-z0-9 _\-\[\]]{1,15}$")]
    private static partial Regex Username();
}
