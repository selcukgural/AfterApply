using AfterApply.Domain.Companies;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanySalaryEntryConfiguration : IEntityTypeConfiguration<CompanySalaryEntry>
{
    public void Configure(EntityTypeBuilder<CompanySalaryEntry> builder)
    {
        builder.ToTable("CompanySalaryEntries");
        builder.HasKey(s => s.Id);

        // Money as numeric(12,2): the domain rounds to two places before the row is written, so
        // nothing is ever silently truncated here.
        builder.Property(s => s.MonthlyNetAmount).HasPrecision(12, 2);
        builder.Property(s => s.AnnualBonusAmount).HasPrecision(12, 2);

        // Strings, as every enum column in this schema (see CompanyReviewConfiguration).
        builder.Property(s => s.EmploymentType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.EmploymentStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.Currency).HasConversion<string>().HasMaxLength(3).IsRequired();

        // One salary per occupation per company per account — enforced where it cannot be raced.
        builder.HasIndex(s => new { s.UserId, s.CompanyId, s.OccupationId }).IsUnique();
        // The company's list, newest first; also the count on the public company page.
        builder.HasIndex(s => new { s.CompanyId, s.SubmittedAt });
        // The quota count and the author's own list.
        builder.HasIndex(s => s.UserId);

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not cascade: a catalogue row with entries behind it is retired (IsActive),
        // never deleted.
        builder.HasOne<Occupation>()
            .WithMany()
            .HasForeignKey(s => s.OccupationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
