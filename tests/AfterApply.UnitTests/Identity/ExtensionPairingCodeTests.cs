using AfterApply.Application.Identity;
using Shouldly;

namespace AfterApply.UnitTests.Identity;

/// <summary>
/// The code is the only part of the pairing flow a human handles, so its rules are about reading
/// and typing rather than about secrecy — that job belongs to the device secret. What is worth
/// pinning: the alphabet excludes the characters people confuse, and normalization never guesses.
/// </summary>
public class ExtensionPairingCodeTests
{
    [Fact]
    public void Generate_Produces_Eight_Characters_From_The_Unambiguous_Alphabet()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = ExtensionPairingCode.Generate();

            code.Length.ShouldBe(ExtensionPairingCode.Length);
            // I/1, L/1 and O/0 are the pairs that get misread off a screen and mistyped into a
            // form; none of them can appear, so there is nothing to misread.
            code.ShouldNotContain("I");
            code.ShouldNotContain("L");
            code.ShouldNotContain("O");
            code.ShouldNotContain("0");
            code.ShouldNotContain("1");
            code.ShouldAllBe(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character));
        }
    }

    [Fact]
    public void Generate_Does_Not_Repeat_Itself()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => ExtensionPairingCode.Generate()).ToHashSet();

        codes.Count.ShouldBe(500);
    }

    [Theory]
    [InlineData("k7m3qxab", "K7M3QXAB")]
    [InlineData("K7M3-QXAB", "K7M3QXAB")]
    [InlineData(" k7m3 qxab ", "K7M3QXAB")]
    public void Normalize_Accepts_The_Forms_A_Person_Produces(string input, string expected)
    {
        ExtensionPairingCode.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_Uppercases_Invariantly()
    {
        // The default culture here is tr-TR, where "i".ToUpper() is "İ" — a character in no
        // alphabet this code uses, and one that would silently drop out of an otherwise valid code.
        ExtensionPairingCode.Normalize("kim3qxab").ShouldBe("KM3QXAB");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("K7M3QXA")]
    [InlineData("K7M3QXABC")]
    [InlineData("K7M3-QX0B")]
    public void IsWellFormed_Rejects_Anything_But_Eight_Alphabet_Characters(string? input)
    {
        ExtensionPairingCode.IsWellFormed(input).ShouldBeFalse();
    }

    [Fact]
    public void Normalize_Drops_Unknown_Characters_Rather_Than_Guessing_At_Lookalikes()
    {
        // "0" is not in the alphabet, so a code typed with one comes out short and is refused.
        // Mapping it to "O" would be the friendly-looking choice and the wrong one: it would turn a
        // typo into a lookup of somebody else's pairing request.
        ExtensionPairingCode.Normalize("K7M30XAB").ShouldBe("K7M3XAB");
        ExtensionPairingCode.IsWellFormed("K7M30XAB").ShouldBeFalse();
    }

    [Fact]
    public void Format_Splits_The_Code_For_Reading()
    {
        ExtensionPairingCode.Format("K7M3QXAB").ShouldBe("K7M3-QXAB");
        ExtensionPairingCode.Normalize(ExtensionPairingCode.Format("K7M3QXAB")).ShouldBe("K7M3QXAB");
    }

    [Fact]
    public void Format_Leaves_A_Code_Of_Unexpected_Length_Alone()
    {
        ExtensionPairingCode.Format("K7M3").ShouldBe("K7M3");
    }
}
