using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Infrastructure.Persistence.Configurations;

public sealed class TelemetryRegistrationConfiguration : IEntityTypeConfiguration<TelemetryRegistration>
{
    public void Configure(EntityTypeBuilder<TelemetryRegistration> builder)
    {
        builder.ToTable("telemetry_registrations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => RegisteredClientId.Create(value));
        builder.Property(e => e.ClientId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CredentialId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Verifier).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Failure).HasMaxLength(200);
        builder.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();
        builder.Ignore(e => e.Status);
        builder.Property(e => e.AccessState).HasConversion<string>().HasMaxLength(16).HasColumnName("access_state");
        builder.Property(e => e.PreviousCredentialId).HasMaxLength(100).HasColumnName("previous_credential_id");
        builder.Property(e => e.PreviousVerifier).HasMaxLength(64).HasColumnName("previous_verifier");
        builder.Property(e => e.RotationId).HasColumnName("rotation_id");
        builder.Property(e => e.PreviousCredentialExpiresAt).HasColumnName("previous_credential_expires_at");
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ClientId).HasColumnName("client_id");
        builder.Property(e => e.OrganizationId).HasColumnName("organization_id");
        builder.Property(e => e.Revision).HasColumnName("revision");
        builder.Property(e => e.AcknowledgedRevision).HasColumnName("acknowledged_revision");
        builder.Property(e => e.CredentialId).HasColumnName("credential_id");
        builder.Property(e => e.Verifier).HasColumnName("verifier");
        builder.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(e => e.Attempts).HasColumnName("attempts");
        builder.Property(e => e.Failure).HasColumnName("failure");
        builder.Property(e => e.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.HasIndex(e => e.NextAttemptAt);
        builder.HasIndex(e => e.OrganizationId);
    }
}
