using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OrganizationClientService
{
    public async Task<TelemetryEnableResult?> EnableTelemetryAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null)
        {
            return null;
        }

        TelemetryConfigurationDto? revealed = null;
        await CommitAndPublishAsync(new TelemetryDesiredChangedEvent { RegistrationId = record.Id.Value }, async token =>
        {
            await TelemetryOwnership.RequireClientAsync(dbContext, record, token);
            await TelemetryOwnership.LockRegistrationAsync(dbContext, record.Id, token);
            TelemetryRegistration? existing = await dbContext.TelemetryRegistrations.AsTracking().FirstOrDefaultAsync(e => e.Id == record.Id, token);
            if (existing is null) { revealed = CreateTelemetry(record); }
            else
            {
                await dbContext.Entry(existing).ReloadAsync(token);
                if (existing.AccessState == TelemetryAccessState.Enabled) { throw new BusinessRuleException(IdentityErrors.TelemetryAlreadyEnabled); }
                (string credentialId, string secret, string verifier) = NewTelemetryCredential();
                existing.Enable(credentialId, verifier, timeProvider);
                revealed = RevealTelemetry(credentialId, secret);
            }
            await dbContext.SaveChangesAsync(token);
        }, ct);
        return new TelemetryEnableResult(await TelemetryStatusAsync(record.Id, ct) ?? throw new InvalidOperationException("Telemetry registration was not persisted."),
            revealed ?? throw new InvalidOperationException("Telemetry configuration was not created."));
    }

    public async Task<TelemetryEnableResult?> RotateTelemetryAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null) { return null; }
        TelemetryRegistration? state = await dbContext.TelemetryRegistrations.AsTracking().FirstOrDefaultAsync(e => e.Id == record.Id, ct);
        if (state is null) { return null; }
        (string credentialId, string secret, string verifier) = NewTelemetryCredential();
        await CommitAndPublishAsync(new TelemetryDesiredChangedEvent { RegistrationId = record.Id.Value },
            async token =>
            {
                await TelemetryOwnership.RequireClientAsync(dbContext, record, token);
                await TelemetryOwnership.LockRegistrationAsync(dbContext, record.Id, token);
                await dbContext.Entry(state).ReloadAsync(token);
                state.Rotate(credentialId, verifier, timeProvider);
                await dbContext.SaveChangesAsync(token);
            }, ct);
        return new TelemetryEnableResult(ToTelemetryStatus(state), RevealTelemetry(credentialId, secret));
    }

    public async Task<TelemetryStatusDto?> RevokeTelemetryAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null) { return null; }
        TelemetryRegistration? state = await dbContext.TelemetryRegistrations.AsTracking().FirstOrDefaultAsync(e => e.Id == record.Id, ct);
        if (state is null) { return null; }
        await CommitAndPublishAsync(new TelemetryDesiredChangedEvent { RegistrationId = record.Id.Value },
            async token =>
            {
                await TelemetryOwnership.RequireClientAsync(dbContext, record, token);
                await TelemetryOwnership.LockRegistrationAsync(dbContext, record.Id, token);
                await dbContext.Entry(state).ReloadAsync(token);
                state.Revoke(deleted: false, timeProvider);
                await dbContext.SaveChangesAsync(token);
            }, ct);
        return ToTelemetryStatus(state);
    }

    private static (string Id, string Secret, string Verifier) NewTelemetryCredential()
    {
        string credentialId = Guid.NewGuid().ToString("N");
        string secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(ClientSecretBytes));
        return (credentialId, secret, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));
    }

    private TelemetryConfigurationDto RevealTelemetry(string credentialId, string secret) => new(
        configuration["Telemetry:IngestionEndpoint"] ?? string.Empty, $"{credentialId}.{secret}", "production", "unknown");

    private TelemetryConfigurationDto CreateTelemetry(RegisteredClient record)
    {
        (string credentialId, string secret, string verifier) = NewTelemetryCredential();
        TelemetryRegistration state = TelemetryRegistration.Create(record, credentialId, verifier, timeProvider);
        if (string.IsNullOrWhiteSpace(configuration["Telemetry:ControlEndpoint"])
            || string.IsNullOrWhiteSpace(configuration["Telemetry:ManagementSecret"])
            || string.IsNullOrWhiteSpace(configuration["Telemetry:IngestionEndpoint"]))
        {
            state.RequireConfiguration(timeProvider);
        }
        dbContext.TelemetryRegistrations.Add(state);
        return RevealTelemetry(credentialId, secret);
    }

    private static TelemetryStatusDto ToTelemetryStatus(TelemetryRegistration state) => new(state.Status, state.Revision, state.AcknowledgedRevision, state.Failure, state.PreviousCredentialExpiresAt);

    private async Task<TelemetryStatusDto?> TelemetryStatusAsync(RegisteredClientId id, CancellationToken ct)
    {
        TelemetryRegistration? state = await dbContext.TelemetryRegistrations.FirstOrDefaultAsync(e => e.Id == id, ct);
        return state is null ? null : ToTelemetryStatus(state);
    }
}
