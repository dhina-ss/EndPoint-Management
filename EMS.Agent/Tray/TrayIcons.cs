using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EMS.Agent.Branding;

namespace EMS.Agent.Tray;

/// <summary>
/// Builds the notification-area icons from The Lance (TL) logo: the logo with a
/// small green check badge when activated, and a dimmed logo with a red badge
/// when not. Falls back to plain colored glyphs if the logo can't be loaded.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TrayIcons
{
    private const int Size = 32;

    // Loaded once; the tray rebuilds icons only on state change. Uses the
    // white-background TL tile so the mark stays legible on any tray colour.
    private static readonly Bitmap? Logo = BrandingAssets.LogoWhite() ?? BrandingAssets.Logo();

    public static Icon Activated() =>
        Build(Color.FromArgb(46, 160, 67), DrawCheck, dim: false);

    public static Icon NotActivated() =>
        Build(Color.FromArgb(218, 54, 51), DrawDot, dim: true);

    private static Icon Build(Color badgeColor, Action<Graphics, RectangleF> badgeGlyph, bool dim)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            if (Logo is not null)
            {
                DrawLogoFitted(g, dim);
            }
            else
            {
                // Fallback: a solid rounded square in the badge colour.
                using var brush = new SolidBrush(badgeColor);
                g.FillEllipse(brush, 1, 1, Size - 2, Size - 2);
            }

            // State badge in the bottom-right corner.
            const float badge = 15f;
            var rect = new RectangleF(Size - badge - 0.5f, Size - badge - 0.5f, badge, badge);
            using var badgeBrush = new SolidBrush(badgeColor);
            using var ring = new Pen(Color.White, 1.5f);
            g.FillEllipse(badgeBrush, rect);
            g.DrawEllipse(ring, rect);
            badgeGlyph(g, rect);
        }

        // Icon.FromHandle does not own the HICON, so clone into a managed icon
        // and destroy the handle to avoid leaking it.
        var hicon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hicon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }

    /// <summary>Draws the logo scaled to fit, preserving aspect ratio.</summary>
    private static void DrawLogoFitted(Graphics g, bool dim)
    {
        var scale = Math.Min((float)Size / Logo!.Width, (float)Size / Logo.Height);
        var w = Logo.Width * scale;
        var h = Logo.Height * scale;
        var x = (Size - w) / 2f;
        var y = (Size - h) / 2f;

        if (dim)
        {
            // Fade the logo when not activated so the state reads at a glance.
            var attrs = new ImageAttributes();
            var matrix = new ColorMatrix { Matrix33 = 0.45f };
            attrs.SetColorMatrix(matrix);
            g.DrawImage(Logo, new Rectangle((int)x, (int)y, (int)w, (int)h),
                0, 0, Logo.Width, Logo.Height, GraphicsUnit.Pixel, attrs);
            attrs.Dispose();
        }
        else
        {
            g.DrawImage(Logo, x, y, w, h);
        }
    }

    private static void DrawCheck(Graphics g, RectangleF b)
    {
        using var pen = new Pen(Color.White, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(pen, new[]
        {
            new PointF(b.Left + b.Width * 0.26f, b.Top + b.Height * 0.52f),
            new PointF(b.Left + b.Width * 0.44f, b.Top + b.Height * 0.70f),
            new PointF(b.Left + b.Width * 0.74f, b.Top + b.Height * 0.30f)
        });
    }

    private static void DrawDot(Graphics g, RectangleF b)
    {
        using var pen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var cx = b.Left + b.Width / 2f;
        var cy = b.Top + b.Height / 2f;
        g.DrawLine(pen, cx, b.Top + b.Height * 0.28f, cx, cy + b.Height * 0.05f);
        g.DrawLine(pen, cx, b.Bottom - b.Height * 0.26f, cx, b.Bottom - b.Height * 0.26f);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
