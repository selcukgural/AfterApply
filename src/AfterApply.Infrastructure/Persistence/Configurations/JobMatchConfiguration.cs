using System.Text.Json;
using AfterApply.Domain.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobMatchConfiguration : IEntityTypeConfiguration<JobMatch>
{
    public void Configure(EntityTypeBuilder<JobMatch> builder)
    {
        builder.ToTable("JobMatches");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.CvTextSnapshot).IsRequired().HasColumnType("text");
        builder.Property(m => m.JobDescription).IsRequired().HasColumnType("text");
        builder.Property(m => m.Recommendation).HasConversion<string>().HasMaxLength(50);

        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        builder.Property(m => m.StrongMatches)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(m => m.Missing)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
            .Metadata.SetValueComparer(stringListComparer);

        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.ApplicationId).IsUnique();

        builder.HasOne<DomainApplication>()
            .WithMany()
            .HasForeignKey(m => m.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
