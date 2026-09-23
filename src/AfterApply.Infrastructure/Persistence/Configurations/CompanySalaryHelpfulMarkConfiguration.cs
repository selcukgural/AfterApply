using AfterApply.Domain.CompanySalaries;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanySalaryHelpfulMarkConfiguration : IEntityTypeConfiguration<CompanySalaryHelpfulMark>
{
    public void Configure(EntityTypeBuilder<CompanySalaryHelpfulMark> builder)
    {
        builder.ToTable("CompanySalaryHelpfulMarks");
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.EntryId, m.UserId }).IsUnique();

        builder.HasOne<CompanySalaryEntry>()
            .WithMany()
            .HasForeignKey(m => m.EntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
