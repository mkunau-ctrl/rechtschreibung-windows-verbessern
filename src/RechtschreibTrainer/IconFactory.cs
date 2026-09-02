using System.Drawing;

namespace RechtschreibTrainer;

/// <summary>
/// Draws the two tray icon states on the fly (red = recording, gray = idle)
/// so the state is visible at a glance without shipping .ico assets.
/// </summary>
internal static class IconFactory
{
    public static Icon CreateIcon(bool recording)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var color = recording ? Color.Crimson : Color.DimGray;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 3, 3, 26, 26);
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
