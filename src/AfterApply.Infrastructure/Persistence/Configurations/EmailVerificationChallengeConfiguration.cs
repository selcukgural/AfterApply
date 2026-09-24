using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class EmailVerificationChallengeConfiguration : IEntityTypeConfiguration<EmailVerificationChallenge>
{
    public void Configure(EntityTypeBuilder<EmailVerificationChallenge> builder)
    {
        builder.ToTable("EmailVerificationChallenges");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TicketHash).IsRequired().HasMaxLength(128);
        builder.Property(c => c.CodeHash).HasMaxLength(128);

        builder.HasIndex(c => c.TicketHash).IsUnique();
        // One pending verification per account.
        builder.HasIndex(c => c.UserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AuthEmailDispatchConfiguration : IEntityTypeConfiguration<AuthEmailDispatch>
{
    public void Configure(EntityTypeBuilder<AuthEmailDispatch> builder)
    {
        builder.ToTable("AuthEmailDispatches");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(d => new { d.UserId, d.Kind, d.SentAt });
        builder.HasIndex(d => new { d.Kind, d.SentAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
