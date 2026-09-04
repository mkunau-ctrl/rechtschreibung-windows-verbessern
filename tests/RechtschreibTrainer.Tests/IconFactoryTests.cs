using System.Drawing;
using System.Drawing.Imaging;
using RechtschreibTrainer;
using Xunit;

namespace RechtschreibTrainer.Tests;

public class IconFactoryTests
{
    private static (TrayState State, bool Recording)[] AllCombinations =>
    [
        (TrayState.Ready, false), (TrayState.Ready, true),
        (TrayState.Paused, false), (TrayState.Paused, true),
        (TrayState.Working, false), (TrayState.Working, true),
    ];

    private static byte[] Pixels(Icon icon)
    {
        using var bmp = icon.ToBitmap();
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    [Fact]
    public void EveryStateLooksDifferent()
    {
        var seen = new List<(string Name, byte[] Pixels)>();

        foreach (var (state, recording) in AllCombinations)
        {
            var name = $"{state}/rec={recording}";
            var pixels = Pixels(IconFactory.Create(state, recording));

            foreach (var (otherName, otherPixels) in seen)
                Assert.False(pixels.SequenceEqual(otherPixels), $"{name} sieht aus wie {otherName}");

            seen.Add((name, pixels));
        }
    }

    [Fact]
    public void ReusesIconsInsteadOfLeakingHandles()
    {
        // Bei jeder Korrektur wird das Icon neu gesetzt — ein neues GDI-Handle
        // pro Aufruf wäre ein Leck.
        var first = IconFactory.Create(TrayState.Ready, recording: false);
        var second = IconFactory.Create(TrayState.Ready, recording: false);

        Assert.Same(first, second);
    }

    [Fact]
    public void RecordingBadgeOnlyChangesTheCorner()
    {
        // Das Grundsymbol muss gleich bleiben, damit der Zustand ablesbar ist;
        // nur die untere rechte Ecke trägt das Mitschreib-Abzeichen.
        using var plain = IconFactory.Create(TrayState.Ready, recording: false).ToBitmap();
        using var badged = IconFactory.Create(TrayState.Ready, recording: true).ToBitmap();

        Assert.Equal(plain.GetPixel(6, 6), badged.GetPixel(6, 6));       // oben links gleich
        Assert.NotEqual(plain.GetPixel(26, 26), badged.GetPixel(26, 26)); // unten rechts anders
    }
}
