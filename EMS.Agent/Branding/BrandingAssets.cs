using System.Drawing;
using System.Reflection;
using System.Runtime.Versioning;

namespace EMS.Agent.Branding;

/// <summary>
/// Loads The Lance branding embedded in the agent assembly (the app icon and
/// the TL logo), for the tray icon and the activation windows.
/// </summary>
[SupportedOSPlatform("windows")]
public static class BrandingAssets
{
    private static readonly Assembly Asm = typeof(BrandingAssets).Assembly;

    private const string IconResource = "EMS.Agent.TheLance.ico";
    private const string LogoResource = "EMS.Agent.Login.thelance-logo.png";
    private const string LogoWhiteResource = "EMS.Agent.thelance-logo-white.png";

    /// <summary>The multi-size TL app icon; falls back to the system icon.</summary>
    public static Icon AppIcon()
    {
        using var stream = Asm.GetManifestResourceStream(IconResource);
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    /// <summary>The transparent TL logo bitmap, or null if unavailable.</summary>
    public static Bitmap? Logo()
    {
        try
        {
            using var stream = Asm.GetManifestResourceStream(LogoResource);
            return stream is not null ? new Bitmap(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The square white-background TL logo (for the tray tile), or null.</summary>
    public static Bitmap? LogoWhite()
    {
        try
        {
            using var stream = Asm.GetManifestResourceStream(LogoWhiteResource);
            return stream is not null ? new Bitmap(stream) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The white TL logo as a data: URI, for self-contained HTML pages.</summary>
    public static string LogoWhiteDataUri()
    {
        try
        {
            using var stream = Asm.GetManifestResourceStream(LogoWhiteResource);
            if (stream is null)
            {
                return string.Empty;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }
}
