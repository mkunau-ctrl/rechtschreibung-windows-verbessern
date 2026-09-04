using RechtschreibTrainer.Core;
using Xunit;

namespace RechtschreibTrainer.Core.Tests;

public class HotkeySpecTests
{
    [Fact]
    public void ParsesASingleFunctionKey()
    {
        var spec = HotkeySpec.Parse("F9");

        Assert.Equal(0u, spec!.Value.Modifiers);
        Assert.Equal(0x78u, spec.Value.VirtualKey); // VK_F9
    }

    [Fact]
    public void ParsesModifiersInGermanOrEnglish()
    {
        var spec = HotkeySpec.Parse("Strg + Alt + K")!.Value;

        Assert.True((spec.Modifiers & HotkeySpec.ModControl) != 0);
        Assert.True((spec.Modifiers & HotkeySpec.ModAlt) != 0);
        Assert.Equal((uint)'K', spec.VirtualKey);
    }

    [Fact]
    public void ParsesADigit()
    {
        Assert.Equal((uint)'3', HotkeySpec.Parse("Ctrl+3")!.Value.VirtualKey);
    }

    [Fact]
    public void IsCaseAndSpaceInsensitive()
    {
        Assert.Equal(HotkeySpec.Parse("STRG+alt+f10"), HotkeySpec.Parse("  strg  +  Alt  +  F10 "));
    }

    [Fact]
    public void ReturnsNullForNonsense()
    {
        Assert.Null(HotkeySpec.Parse(""));
        Assert.Null(HotkeySpec.Parse("Strg+"));
        Assert.Null(HotkeySpec.Parse("Blafasel"));
    }

    [Fact]
    public void RendersBackToAReadableString()
    {
        Assert.Equal("Strg+Alt+F10", HotkeySpec.Parse("strg+alt+f10")!.Value.ToString());
        Assert.Equal("F9", HotkeySpec.Parse("F9")!.Value.ToString());
    }
}
