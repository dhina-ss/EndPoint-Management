using System.Security.Cryptography;
using System.Text;

namespace EMS.API.Services;

/// <summary>
/// Token generation and hashing shared by issue and validation paths.
/// </summary>
internal static class DeviceTokenHasher
{
    private const int TokenSizeBytes = 32;

    /// <summary>Creates a cryptographically random, URL-safe device token.</summary>
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>Base64 SHA-256 of the token; this is what gets persisted.</summary>
    public static string Hash(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>Constant-time comparison to avoid timing side channels.</summary>
    public static bool HashEquals(string expectedHash, string actualHash)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(actualHash));
    }
}
