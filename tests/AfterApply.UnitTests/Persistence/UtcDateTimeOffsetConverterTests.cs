using AfterApply.Infrastructure.Persistence.Converters;
using Shouldly;

namespace AfterApply.UnitTests.Persistence;

public class UtcDateTimeOffsetConverterTests
{
    private static readonly UtcDateTimeOffsetConverter Converter = new();

    [Fact]
    public void Write_Moves_An_Offset_Timestamp_To_Utc_Without_Moving_The_Instant()
    {
        // The case that used to reach Npgsql and come back as a 500: a caller in Istanbul writing
        // 09:00 local. It is the same moment as 06:00Z and has to be stored as such, not rejected.
        var istanbulMorning = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(3));

        var stored = (DateTimeOffset)Converter.ConvertToProvider(istanbulMorning)!;

        stored.Offset.ShouldBe(TimeSpan.Zero);
        stored.ShouldBe(istanbulMorning);
        stored.UtcDateTime.ShouldBe(new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Write_Handles_An_Offset_Behind_Utc_Too()
    {
        var newYorkEvening = new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.FromHours(-4));

        var stored = (DateTimeOffset)Converter.ConvertToProvider(newYorkEvening)!;

        stored.Offset.ShouldBe(TimeSpan.Zero);
        stored.UtcDateTime.ShouldBe(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Write_Leaves_A_Utc_Timestamp_Exactly_As_It_Is()
    {
        // Which is every timestamp the app writes itself, so the common path stays a no-op.
        var utc = new DateTimeOffset(2026, 9, 1, 6, 0, 0, TimeSpan.Zero);

        Converter.ConvertToProvider(utc).ShouldBe(utc);
    }

    [Fact]
    public void Read_Is_The_Identity()
    {
        // Postgres hands a timestamptz back at offset zero on its own; there is nothing to undo.
        var fromDatabase = new DateTimeOffset(2026, 9, 1, 6, 0, 0, TimeSpan.Zero);

        Converter.ConvertFromProvider(fromDatabase).ShouldBe(fromDatabase);
    }

    [Fact]
    public void Write_Then_Read_Round_Trips_The_Instant()
    {
        var istanbulMorning = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(3));

        var roundTripped = (DateTimeOffset)Converter.ConvertFromProvider(
            Converter.ConvertToProvider(istanbulMorning))!;

        roundTripped.ShouldBe(istanbulMorning);
    }
}
