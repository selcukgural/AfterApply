using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AfterApply.Infrastructure.Persistence.Converters;

/// <summary>
/// Moves every <see cref="DateTimeOffset" /> to UTC on its way into Postgres.
/// </summary>
/// <remarks>
/// Npgsql refuses to write a <c>DateTimeOffset</c> whose offset is anything but zero to a
/// <c>timestamp with time zone</c> column, and it refuses it at write time — so a client posting a
/// perfectly valid instant as <c>2026-09-01T09:00:00+03:00</c> got a 500 out of SaveChanges rather
/// than a 400 out of validation. Rejecting those payloads would be the wrong fix: an offset-carrying
/// timestamp names the same instant as its UTC twin, and <c>timestamptz</c> stores instants — the
/// offset was never going to be persisted either way. So the write normalises instead, which is
/// exactly what the column already does to the values it accepts.
/// <para>
/// Reading back is the identity: Postgres hands every <c>timestamptz</c> back at offset zero, so a
/// converted value and a natively-read one are indistinguishable.
/// </para>
/// </remarks>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value)
    {
    }
}
