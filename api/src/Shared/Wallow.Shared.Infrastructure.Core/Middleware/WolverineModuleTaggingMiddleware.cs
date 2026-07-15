using System.Diagnostics;
using System.Text.RegularExpressions;
using Wallow.Shared.Kernel;
using Wallow.Shared.Kernel.Domain;
using Wolverine;

namespace Wallow.Shared.Infrastructure.Core.Middleware;

public static partial class WolverineModuleTaggingMiddleware
{
    private const string StartTimestampKey = "wallow.messaging.start_timestamp";

    public static void Before(Envelope envelope)
    {
        if (envelope.Message is null)
        {
            return;
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        envelope.Headers[StartTimestampKey] = startTimestamp.ToString();

        Type messageType = envelope.Message.GetType();

        // Tag the current activity with module and tenant info
        if (Activity.Current is { } activity)
        {
            string? ns = messageType.Namespace;
            if (ns is not null)
            {
                Match match = ModuleNamePattern().Match(ns);
                if (match.Success)
                {
                    activity.SetTag("wallow.module", match.Groups[1].Value);
                }
            }

            if (envelope.Headers.TryGetValue("X-Tenant-Id", out string? tenantId)
                && !string.IsNullOrEmpty(tenantId))
            {
                activity.SetTag("wallow.tenant_id", tenantId);
            }
        }

        // Track domain event publishing
        if (envelope.Message is IDomainEvent)
        {
            Diagnostics.DomainEventsPublishedTotal.Add(1,
                new KeyValuePair<string, object?>("event_type", messageType.Name));
        }
    }

    public static void After(Envelope envelope)
    {
        RecordMetrics(envelope, "success");
    }

    private static void RecordMetrics(Envelope envelope, string status)
    {
        if (envelope.Message is null)
        {
            return;
        }

        Type messageType = envelope.Message.GetType();
        string module = ResolveModule(messageType);

        KeyValuePair<string, object?>[] tags =
        [
            new("message_type", messageType.Name),
            new("module", module),
            new("status", status)
        ];

        Diagnostics.MessagesTotal.Add(1, tags);

        if (envelope.Headers.TryGetValue(StartTimestampKey, out string? startValue)
            && long.TryParse(startValue, out long startTimestamp))
        {
            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            Diagnostics.MessageDuration.Record(elapsedMs, tags);
        }
    }

    private static string ResolveModule(Type messageType)
    {
        string? ns = messageType.Namespace;
        if (ns is null)
        {
            return "unknown";
        }

        Match match = ModuleNamePattern().Match(ns);
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    [GeneratedRegex(@"^Wallow\.(\w+)\.(Application|Infrastructure)\b", RegexOptions.NonBacktracking)]
    private static partial Regex ModuleNamePattern();
}
