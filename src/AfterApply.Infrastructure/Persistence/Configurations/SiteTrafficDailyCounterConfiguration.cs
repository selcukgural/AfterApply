using AfterApply.Domain.SiteTraffic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class SiteTrafficDailyCounterConfiguration : IEntityTypeConfiguration<SiteTrafficDailyCounter>
{
    public void Configure(EntityTypeBuilder<SiteTrafficDailyCounter> builder)
    {
        builder.ToTable("SiteTrafficDailyCounters");
        builder.HasKey(c => c.Id);

        // Stored as text, like every other enum in this schema: adding an event becomes a code
        // change instead of a migration, and a row stays readable in psql.
        builder.Property(c => c.Event).HasConversion<string>().HasMaxLength(64).IsRequired();

        builder.Property(c => c.Path).HasMaxLength(SiteTrafficLimits.PathColumnLength).IsRequired();
        builder.Property(c => c.Locale).HasMaxLength(8).IsRequired();
        builder.Property(c => c.ReferrerHost).HasMaxLength(SiteTrafficLimits.HostColumnLength).IsRequired();

        // The upsert key, and the reason SiteTrafficService can use ON CONFLICT. Unique so the
        // database — not the service — is what guarantees one row per combination per day.
        builder.HasIndex(c => new { c.Day, c.Event, c.Path, c.Locale, c.ReferrerHost }).IsUnique();

        // No foreign key to Users, because there is no UserId to hang one on: this table holds
        // counts of anonymous visits and is outside the cascade-from-Users rule that every
        // user-owned table follows (DECISIONS.md 2026-09-07). Nothing is missing here.
    }
}

/// <summary>Column widths, kept next to the configuration that applies them so the migration and
/// the validator cannot drift apart silently.</summary>
internal static class SiteTrafficLimits
{
    public const int PathColumnLength = 256;
    public const int HostColumnLength = 100;
}
