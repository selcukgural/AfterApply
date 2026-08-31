using AfterApply.Domain.EmailIntegrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class EmailSuggestionConfiguration : IEntityTypeConfiguration<EmailSuggestion>
{
    public void Configure(EntityTypeBuilder<EmailSuggestion> builder)
    {
        builder.ToTable("EmailSuggestions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProviderMessageId).IsRequired().HasMaxLength(256);
        builder.Property(s => s.ProviderThreadId).HasMaxLength(256);
        builder.Property(s => s.SuggestedStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.MatchedRule).IsRequired().HasMaxLength(100);
        builder.Property(s => s.SenderDomain).HasMaxLength(256);
        builder.Property(s => s.Subject).HasMaxLength(500);
        builder.Property(s => s.Snippet).HasMaxLength(2000);
        builder.Property(s => s.ExtractedCompanyName).HasMaxLength(300);
        builder.Property(s => s.ExtractedJobTitle).HasMaxLength(300);
        builder.Property(s => s.ExtractedLocation).HasMaxLength(300);
        builder.Property(s => s.ExtractedDescription).HasColumnType("text");
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => new { s.UserId, s.Status });
        builder.HasIndex(s => new { s.EmailConnectionId, s.ProviderMessageId }).IsUnique();

        builder.HasOne<EmailConnection>()
            .WithMany()
            .HasForeignKey(s => s.EmailConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<DomainApplication>()
            .WithMany()
            .HasForeignKey(s => s.ApplicationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
