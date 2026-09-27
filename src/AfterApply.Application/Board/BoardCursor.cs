using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace AfterApply.Application.Board;

/// <summary>
/// The keyset position a column page ends at: the last card's (Position, Id). Opaque to the client
/// — it is echoed back, never built — so the encoding can change without a contract change.
/// Keyset rather than page numbers because cards arrive on top while the user scrolls: an offset
/// would shift under them and show a card twice.
/// </summary>
public readonly record struct BoardCursor(long Position, Guid CardId)
{
    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{Position}:{CardId:N}")));

    public static bool TryDecode(string? value, out BoardCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            return false;
        }

        string text;
        try
        {
            text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
        }
        catch (FormatException)
        {
            return false;
        }

        var separator = text.IndexOf(':');
        if (separator <= 0
            || !long.TryParse(text.AsSpan(0, separator), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var position)
            || !Guid.TryParseExact(text.AsSpan(separator + 1), "N", out var cardId))
        {
            return false;
        }

        cursor = new BoardCursor(position, cardId);
        return true;
    }
}
