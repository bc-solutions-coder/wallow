using Microsoft.EntityFrameworkCore;

namespace Wallow.TelemetryGateway;

internal sealed class RegistryDb(DbContextOptions<RegistryDb> options) : DbContext(options)
{
    public DbSet<RegistrationEntry> Registrations => Set<RegistrationEntry>();

    public DbSet<CredentialEntry> Credentials => Set<CredentialEntry>();

    public DbSet<RotationEntry> Rotations => Set<RotationEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegistrationEntry>().HasKey(entry => entry.Id);
        modelBuilder.Entity<CredentialEntry>().HasKey(entry => entry.Id);
        modelBuilder.Entity<CredentialEntry>().HasIndex(entry => entry.RegistrationId);
        modelBuilder.Entity<RotationEntry>().HasKey(entry => entry.Id);
    }
}

internal sealed class RegistrationEntry
{
    public Guid Id { get; set; }

    public long Revision { get; set; }

    public string Desired { get; set; } = string.Empty;

    public string Acknowledgement { get; set; } = string.Empty;
}

internal sealed class CredentialEntry
{
    public string Id { get; set; } = string.Empty;

    public Guid RegistrationId { get; set; }

    public string Verifier { get; set; } = string.Empty;

    public bool Revoked { get; set; }

    public long? ExpiresAt { get; set; }
}

internal sealed class RotationEntry
{
    public Guid Id { get; set; }

    public Guid RegistrationId { get; set; }

    public string PreviousCredentialId { get; set; } = string.Empty;

    public string NewCredentialId { get; set; } = string.Empty;

    public long Deadline { get; set; }
}
