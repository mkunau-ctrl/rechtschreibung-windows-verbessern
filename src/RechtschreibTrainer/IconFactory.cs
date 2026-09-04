using System.Drawing;
using System.Drawing.Drawing2D;

namespace RechtschreibTrainer;

/// <summary>Zustand der Live-Korrektur. Mitschreiben ist unabhängig davon (Abzeichen).</summary>
internal enum TrayState
{
    /// <summary>Live-Korrektur bereit.</summary>
    Ready,
    /// <summary>Korrigiert gerade.</summary>
    Working,
    /// <summary>Live-Korrektur pausiert.</summary>
    Paused,
}

/// <summary>
/// Zeichnet die Tray-Icons zur Laufzeit — abgerundetes Quadrat als Träger,
/// darin ein weißes Symbol für den Zustand. Die Bedeutung steckt in der Form,
/// nicht nur in der Farbe, damit der Zustand auch bei Farbsehschwäche und in
/// 16×16 ablesbar bleibt. Mitschreiben kommt als roter Punkt unten rechts dazu,
/// weil es unabhängig von der Korrektur an sein kann.
///
/// Icons werden gecacht: das Tray-Icon wird bei jeder Korrektur neu gesetzt,
/// und jedes <c>GetHicon</c> wäre sonst ein weiteres nie freigegebenes
/// GDI-Handle.
/// </summary>
internal static class IconFactory
{
    private static readonly Dictionary<(TrayState, bool), Icon> Cache = new();

    private static readonly Color Green = Color.FromArgb(0x16, 0xA3, 0x4A);
    private static readonly Color Blue = Color.FromArgb(0x25, 0x63, 0xEB);
    private static readonly Color Grey = Color.FromArgb(0x6B, 0x72, 0x80);
    private static readonly Color RecordRed = Color.FromArgb(0xDC, 0x26, 0x26);

    public static Icon Create(TrayState state, bool recording)
    {
        if (Cache.TryGetValue((state, recording), out var cached))
            return cached;

        var icon = Draw(state, recording);
        Cache[(state, recording)] = icon;
        return icon;
    }

    private static Icon Draw(TrayState state, bool recording)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            DrawTile(g, BaseColor(state));
            DrawGlyph(g, state);

            if (recording)
                DrawRecordBadge(g);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static Color BaseColor(TrayState state) => state switch
    {
        TrayState.Ready => Green,
        TrayState.Working => Blue,
        _ => Grey,
    };

    /// <summary>Abgerundetes Quadrat als Träger des Symbols.</summary>
    private static void DrawTile(Graphics g, Color color)
    {
        using var path = RoundedRect(new Rectangle(2, 2, 28, 28), radius: 8);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    private static void DrawGlyph(Graphics g, TrayState state)
    {
        using var pen = new Pen(Color.White, 4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var white = new SolidBrush(Color.White);

        switch (state)
        {
            case TrayState.Ready: // Häkchen
                g.DrawLines(pen, new[] { new Point(9, 16), new Point(14, 22), new Point(23, 10) });
                break;

            case TrayState.Paused: // Minus-Balken
                g.DrawLine(pen, 10, 16, 22, 16);
                break;

            case TrayState.Working: // drei Punkte
                foreach (var x in new[] { 8, 15, 22 })
                    g.FillEllipse(white, x, 14, 5, 5);
                break;
        }
    }

    /// <summary>Roter Punkt unten rechts, weiß abgesetzt, damit er sich vom Träger löst.</summary>
    private static void DrawRecordBadge(Graphics g)
    {
        using var ring = new SolidBrush(Color.White);
        using var dot = new SolidBrush(RecordRed);
        g.FillEllipse(ring, 18, 18, 14, 14);
        g.FillEllipse(dot, 20, 20, 10, 10);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
