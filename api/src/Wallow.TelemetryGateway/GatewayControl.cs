using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Wallow.TelemetryGateway;

public static class GatewayControl
{
    public static async Task<WebApplication> CreateAsync(string databasePath, string managementSecret, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managementSecret);
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 64 * 1024;
            options.Limits.MaxConcurrentConnections = 16;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddDbContext<RegistryDb>(options => options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddSingleton(_ => new SemaphoreSlim(1, 1));
        WebApplication app = builder.Build();
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            RegistryDb db = scope.ServiceProvider.GetRequiredService<RegistryDb>();
            await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        SemaphoreSlim updates = app.Services.GetRequiredService<SemaphoreSlim>();
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes($"Bearer {managementSecret}"));
        app.Use(async (context, next) =>
        {
            byte[] supplied = SHA256.HashData(Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString()));
            if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (context.Request.Headers.ContentEncoding.Count > 0)
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            await updates.WaitAsync(context.RequestAborted).ConfigureAwait(false);
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                updates.Release();
            }
        });
        app.MapPut("/control/v1/registrations/{registrationId:guid}", async (
            Guid registrationId, DesiredRegistration desired, RegistryDb db, CancellationToken cancellationToken) =>
        {
            if (registrationId == Guid.Empty || !RegistrationValidation.IsValid(desired))
            {
                return Results.BadRequest();
            }

            string content = JsonSerializer.Serialize(desired);
            RegistrationEntry? existing = await db.Registrations.FindAsync([registrationId], cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.Revision == desired.Revision && string.Equals(existing.Desired, content, StringComparison.Ordinal))
                {
                    return Results.Content(existing.Acknowledgement, "application/json");
                }

                DesiredRegistration previous = JsonSerializer.Deserialize<DesiredRegistration>(existing.Desired)!;
                if (desired.Revision <= existing.Revision || previous.State == RegistrationState.Deleted)
                {
                    return Results.Conflict();
                }
            }

            List<CredentialEntry> known = await db.Credentials.Where(credential => credential.RegistrationId == registrationId)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            RotationEntry? rotation = null;
            if (desired.Rotation is not null)
            {
                CredentialRotation operation = desired.Rotation;
                if (operation.OperationId == Guid.Empty || desired.State != RegistrationState.Enabled
                    || string.Equals(operation.PreviousCredentialId, operation.NewCredentialId, StringComparison.Ordinal)
                    || !desired.Credentials.Any(credential => string.Equals(credential.Id, operation.PreviousCredentialId, StringComparison.Ordinal))
                    || !desired.Credentials.Any(credential => string.Equals(credential.Id, operation.NewCredentialId, StringComparison.Ordinal)))
                {
                    return Results.BadRequest();
                }

                rotation = await db.Rotations.FindAsync([operation.OperationId], cancellationToken).ConfigureAwait(false);
                if (rotation is not null && (rotation.RegistrationId != registrationId
                    || !string.Equals(rotation.PreviousCredentialId, operation.PreviousCredentialId, StringComparison.Ordinal)
                    || !string.Equals(rotation.NewCredentialId, operation.NewCredentialId, StringComparison.Ordinal)))
                {
                    return Results.Conflict();
                }

                if (rotation is null)
                {
                    CredentialEntry? previous = known.Find(credential => string.Equals(credential.Id, operation.PreviousCredentialId, StringComparison.Ordinal));
                    if (previous is null || previous.Revoked || previous.ExpiresAt is not null
                        || await db.Credentials.AnyAsync(credential => credential.Id == operation.NewCredentialId, cancellationToken).ConfigureAwait(false))
                    {
                        return Results.Conflict();
                    }

                    rotation = new RotationEntry
                    {
                        Id = operation.OperationId,
                        RegistrationId = registrationId,
                        PreviousCredentialId = operation.PreviousCredentialId,
                        NewCredentialId = operation.NewCredentialId,
                        Deadline = clock.GetUtcNow().AddHours(24).ToUnixTimeMilliseconds(),
                    };
                    previous.ExpiresAt = rotation.Deadline;
                    db.Rotations.Add(rotation);
                }
            }

            foreach (DesiredCredential credential in desired.Credentials)
            {
                CredentialEntry? stored = await db.Credentials.FindAsync([credential.Id], cancellationToken).ConfigureAwait(false);
                if (stored is not null && (stored.RegistrationId != registrationId || (stored.Revoked && desired.State == RegistrationState.Enabled)
                    || !string.Equals(stored.Verifier, credential.Verifier, StringComparison.OrdinalIgnoreCase)))
                {
                    return Results.Conflict();
                }

                if (stored is null)
                {
                    stored = new CredentialEntry { Id = credential.Id, RegistrationId = registrationId, Verifier = credential.Verifier };
                    db.Credentials.Add(stored);
                    known.Add(stored);
                }
            }

            foreach (CredentialEntry credential in known)
            {
                credential.Revoked = credential.Revoked || desired.State != RegistrationState.Enabled
                    || !desired.Credentials.Any(candidate => string.Equals(candidate.Id, credential.Id, StringComparison.Ordinal));
            }

            string acknowledgement = JsonSerializer.Serialize(new RegistrationAcknowledgement(registrationId, desired.Revision, clock.GetUtcNow(),
                rotation is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(rotation.Deadline)));
            if (existing is null)
            {
                existing = new RegistrationEntry { Id = registrationId };
                db.Registrations.Add(existing);
            }

            existing.Revision = desired.Revision;
            existing.Desired = content;
            existing.Acknowledgement = acknowledgement;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Results.Content(acknowledgement, "application/json");
        });
        PrivateSourceMaps.Map(app, databasePath);
        return app;
    }
}
