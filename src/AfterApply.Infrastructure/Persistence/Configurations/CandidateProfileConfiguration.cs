using AfterApply.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("CandidateProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CvText).IsRequired().HasColumnType("text");

        builder.HasIndex(p => p.UserId).IsUnique();
    }
}
