using RechtschreibTrainer;
using Xunit;

namespace RechtschreibTrainer.Tests;

public class ReplacerTests
{
    /// <summary>
    /// SendInput prüft die übergebene Strukturgröße und verwirft die Eingabe
    /// kommentarlos, wenn sie nicht exakt der Win32-Definition entspricht.
    /// INPUT enthält ein Union aus MOUSEINPUT / KEYBDINPUT / HARDWAREINPUT und
    /// ist damit so groß wie MOUSEINPUT: 40 Byte auf x64, 28 Byte auf x86.
    /// </summary>
    [Fact]
    public void InputStructMatchesTheWin32Size()
    {
        var expected = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expected, Replacer.InputStructSize);
    }
}
