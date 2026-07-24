using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexQuotaDashboard;

public static class TrayIconRenderer
{
    public static Icon Render(QuotaSnapshot quota, DashboardSettings settings, double arcOpacity = 1)
    {
        const int size = 128;
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        var remaining = quota.RemainingPercent;
        var baseColor = GetRingColor(remaining);
        var alpha = (int)(baseColor.A * Math.Clamp(arcOpacity, 0, 1));
        var ringColor = Color.FromArgb(alpha, baseColor);
        var width = (float)Math.Clamp(settings.RingThickness * 4, 8, 32);
        var inset = width / 2 + 1.25f;
        var rect = new RectangleF(inset, inset, size - inset * 2, size - inset * 2);

        using (var pen = new Pen(ringColor, width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (remaining is null)
            {
                graphics.DrawArc(pen, rect, -70, 280);
            }
            else
            {
                var sweep = (float)(Math.Clamp(remaining.Value, 1.5, 99.2) * 3.6);
                var gap = 360 - sweep;
                const float gapCenter = 225;
                var start = gapCenter + gap / 2;
                graphics.DrawArc(pen, rect, start, sweep);
            }
        }

        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    public static Color GetRingColor(double? remaining) => remaining switch
    {
        null => ColorTranslator.FromHtml("#8A909C"),
        >= 75 => ColorTranslator.FromHtml("#35D07F"),
        >= 50 => ColorTranslator.FromHtml("#00A8FF"),
        >= 25 => ColorTranslator.FromHtml("#FF9F2F"),
        _ => ColorTranslator.FromHtml("#FF4D5A")
    };

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
