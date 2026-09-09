using System.Security.Cryptography;

namespace AfterApply.Application.Identity;

/// <summary>
/// The short code a person reads off the extension and confirms on the web page. It is an
/// identifier, not a credential: approving one requires a signed-in session, and collecting the
/// token it leads to requires the device secret the extension keeps to itself. What the code has
/// to be is *readable* — someone may end up comparing two of them character by character on a
/// phishing-shaped day, which is why the alphabet drops every pair that looks alike in a sans-serif
/// font (I/1, O/0) and why the display form is split in two halves.
/// </summary>
public static class ExtensionPairingCode
{
    /// <summary>No I, L, O, 0 or 1. 31 symbols over 8 characters is ~39.6 bits — far past what the
    /// per-IP rate limit and the ten-minute lifetime leave room to guess.</summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int Length = 8;

    public static string Generate() => new(RandomNumberGenerator.GetItems<char>(Alphabet, Length));

    /// <summary>"k7m3-qxab", "K7M3 QXAB" and "K7M3QXAB" are the same code. Anything outside the
    /// alphabet is dropped rather than mapped to a lookalike: a dropped character produces a
    /// wrong-length code and an honest "that code is not valid", where guessing at the user's
    /// intent could silently look up somebody else's pairing.</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        // ToUpperInvariant, never ToUpper(): the default culture here is tr-TR, where "i" uppercases
        // to a dotted "İ" that is in no alphabet this code uses.
        return new string(code.ToUpperInvariant().Where(Alphabet.Contains).ToArray());
    }

    public static bool IsWellFormed(string? code) => Normalize(code).Length == Length;

    /// <summary>Display form, "K7M3-QXAB". Split for reading aloud and for comparing at a glance;
    /// <see cref="Normalize"/> undoes it.</summary>
    public static string Format(string code) =>
        code.Length == Length ? $"{code[..4]}-{code[4..]}" : code;
}
