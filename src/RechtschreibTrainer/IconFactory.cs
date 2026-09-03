using System.Drawing;

namespace RechtschreibTrainer;

internal enum TrayState
{
    /// <summary>Live-Korrektur bereit.</summary>
    Ready,
    /// <summary>Korrigiert gerade / wartet.</summary>
    Working,
    /// <summary>Live-Korrektur pausiert.</summary>
    Paused,
    /// <summary>Mitschreib-Modus aktiv.</summary>
    Recording,
}

/// <summary>
/// Zeichnet die Tray-Icon-Zustände zur Laufzeit, damit der Status auf einen
/// Blick sichtbar ist, ohne .ico-Dateien mitzuliefern.
/// </summary>
internal static class IconFactory
{
    public static Icon Create(TrayState state)
    {
        var color = state switch
        {
            TrayState.Ready => Color.LimeGreen,
            TrayState.Working => Color.DodgerBlue,
            TrayState.Recording => Color.Crimson,
            _ => Color.DimGray,
        };

        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 3, 3, 26, 26);
        }

        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }
}
