using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class TelemetryReconciliationService(IServiceScopeFactory scopes, IConfiguration configuration,
    TimeProvider clock, ILogger<TelemetryReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (string.IsNullOrWhiteSpace(configuration["Telemetry:ControlEndpoint"]))
            {
                continue;
            }

            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                DateTimeOffset now = clock.GetUtcNow();
                List<RegisteredClientId> due = await db.TelemetryRegistrations
                    .Where(e => e.NextAttemptAt <= now).OrderBy(e => e.NextAttemptAt)
                    .Select(e => e.Id).Take(100).ToListAsync(stoppingToken);
                TelemetryProvisioner provisioner = scope.ServiceProvider.GetRequiredService<TelemetryProvisioner>();
                foreach (RegisteredClientId id in due)
                {
                    await provisioner.ReconcileAsync(id.Value, stoppingToken);
                    db.ChangeTracker.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogReconciliationFailed(ex.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telemetry reconciliation will retry after {FailureType}")]
    private partial void LogReconciliationFailed(string failureType);
}
