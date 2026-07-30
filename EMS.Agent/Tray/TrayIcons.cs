using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EMS.Agent.Tray;

/// <summary>
/// Builds the notification-area icons at runtime (no shipped .ico files): a
/// green circle with a check when activated, a red circle with a cross when not.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TrayIcons
{
    private const int Size = 32;

    public static Icon Activated() =>
        Build(Color.FromArgb(46, 160, 67), DrawCheck);

    public static Icon NotActivated() =>
        Build(Color.FromArgb(218, 54, 51), DrawCross);

    private static Icon Build(Color circle, Action<Graphics> glyph)
    {
        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(circle);
            g.FillEllipse(brush, 1, 1, Size - 2, Size - 2);
            glyph(g);
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

    private static void DrawCheck(Graphics g)
    {
        using var pen = new Pen(Color.White, 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(pen, new[] { new PointF(9, 17), new PointF(14, 22), new PointF(23, 10) });
    }

    private static void DrawCross(Graphics g)
    {
        using var pen = new Pen(Color.White, 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(pen, 11, 11, 21, 21);
        g.DrawLine(pen, 21, 11, 11, 21);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
