using System.Text.RegularExpressions;

namespace EMS.API.Services;

/// <summary>
/// Turns whatever the admin typed — a full URL, a "www." host, or a bare
/// domain — into a canonical bare host suitable for a hosts-file entry.
/// </summary>
public static partial class DomainNormalizer
{
    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9](-?[a-z0-9])*\.)+[a-z]{2,}$")]
    private static partial Regex HostPattern();

    /// <summary>
    /// Returns the normalized host, or null if the input is not a valid
    /// public domain. The leading "www." is preserved as typed — blocking
    /// "www.example.com" specifically is a legitimate choice — but scheme,
    /// path, port and query are stripped.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim().ToLowerInvariant();

        // Strip scheme if present so Uri parsing is not required.
        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            value = value[(schemeIndex + 3)..];
        }

        // Drop anything after the host: path, query, or fragment.
        value = value.Split('/', '?', '#')[0];

        // Drop a port suffix and any userinfo.
        value = value.Split('@')[^1];
        value = value.Split(':')[0];

        value = value.TrimEnd('.');

        return HostPattern().IsMatch(value) ? value : null;
    }
}
