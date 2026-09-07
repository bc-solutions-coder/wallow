using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>Reconciles the latest persisted revision; optimistic concurrency rejects stale delivery results.</summary>
public sealed class TelemetryProvisioner(IdentityDbContext db, IHttpClientFactory clients, IConfiguration configuration, TimeProvider clock)
{
    private static readonly string[] _environments = ["development", "staging", "production", "test"];

    public async Task ReconcileAsync(Guid registrationId, CancellationToken ct)
    {
        RegisteredClientId id = RegisteredClientId.Create(registrationId);
        TelemetryRegistration? state = await db.TelemetryRegistrations.AsTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (state is null)
        {
            return;
        }

        string? endpoint = configuration["Telemetry:ControlEndpoint"];
        string? managementSecret = configuration["Telemetry:ManagementSecret"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? target) || string.IsNullOrWhiteSpace(managementSecret)
            || string.IsNullOrWhiteSpace(configuration["Telemetry:IngestionEndpoint"]))
        {
            state.RequireConfiguration(clock);
            await SaveResultAsync(ct);
            return;
        }

        using HttpClient client = clients.CreateClient("telemetry-control");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managementSecret);
        Uri route = new(target, $"/control/v1/registrations/{registrationId}");
        try
        {
            using HttpResponseMessage response = await client.PutAsJsonAsync(route, new
            {
                revision = state.Revision,
                clientId = state.ClientId,
                applicationId = state.ClientId,
                browserService = $"{state.ClientId}-browser",
                serverService = $"{state.ClientId}-server",
                environments = _environments,
                state = state.AccessState.ToString(),
                credentials = state.AccessState != TelemetryAccessState.Enabled ? []
                    : state.RotationId is null ? new[] { new { id = state.CredentialId, verifier = state.Verifier } }
                    : new[] { new { id = state.PreviousCredentialId!, verifier = state.PreviousVerifier! }, new { id = state.CredentialId, verifier = state.Verifier } },
                rotation = state.RotationId is null ? null : new { operationId = state.RotationId, previousCredentialId = state.PreviousCredentialId, newCredentialId = state.CredentialId },
            }, ct);
            if (response.IsSuccessStatusCode)
            {
                await response.Content.LoadIntoBufferAsync(64 * 1024, ct);
                ProvisioningAcknowledgement? acknowledgement = await response.Content.ReadFromJsonAsync<ProvisioningAcknowledgement>(ct);
                if (acknowledgement?.RegistrationId == registrationId && acknowledgement.Revision == state.Revision
                    && (state.RotationId is null || acknowledgement.PreviousCredentialExpiresAt is not null))
                {
                    state.Acknowledge(acknowledgement.Revision, clock, acknowledgement.PreviousCredentialExpiresAt);
                }
                else
                {
                    state.Retry(permanent: true, clock);
                }
            }
            else
            {
                bool permanent = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity;
                state.Retry(permanent, clock);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            state.Retry(permanent: false, clock);
        }

        await SaveResultAsync(ct);
    }

    private async Task SaveResultAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
        }
    }

    private sealed record ProvisioningAcknowledgement(Guid RegistrationId, long Revision, DateTimeOffset? PreviousCredentialExpiresAt);
}
