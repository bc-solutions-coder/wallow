using Serilog.Core;
using Serilog.Events;
using Wallow.Api.Middleware;
using Wallow.ServiceDefaults;

namespace Wallow.Api.Logging;

/// <summary>Exports stable events and sanitized properties without rendering message templates or exception messages.</summary>
internal sealed class IndependentTelemetrySink(IndependentTelemetry telemetry) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        try { EmitCore(logEvent); }
        catch (Exception) { telemetry.ReportCaptureFailure(); }
    }

    private void EmitCore(LogEvent logEvent)
    {
        int remaining = 256;
        Dictionary<string, object?> attributes = new(StringComparer.Ordinal);
        foreach ((string key, LogEventPropertyValue value) in logEvent.Properties.Take(32))
        {
            attributes[key] = Value(value, 0, ref remaining);
        }
        bool requestFailure = logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? source)
            && source is ScalarValue { Value: "Serilog.AspNetCore.RequestLoggingMiddleware" };
        int? expectedStatus = requestFailure && logEvent.Exception is not null ? GlobalExceptionHandler.GetStatusCode(logEvent.Exception) : null;
        LogEventLevel eventLevel = expectedStatus < 500 ? LogEventLevel.Warning : logEvent.Level;
        if (expectedStatus < 500 && attributes.ContainsKey("StatusCode"))
        {
            attributes["StatusCode"] = expectedStatus;
        }
        string name = logEvent.Exception is null ? "application.log"
            : eventLevel >= LogEventLevel.Error ? "exception.unexpected" : "application.failure.handled";
        LogLevel level = eventLevel switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            _ => LogLevel.Critical,
        };
        telemetry.WriteLog(level, name, attributes, logEvent.Exception);
    }

    private static object? Value(LogEventPropertyValue value, int depth, ref int remaining)
    {
        if (--remaining < 0 || depth > 5)
        {
            return "[limit]";
        }

        if (value is ScalarValue scalar)
        {
            return scalar.Value;
        }

        if (value is SequenceValue sequence)
        {
            List<object?> values = [];
            foreach (LogEventPropertyValue item in sequence.Elements.Take(32))
            {
                if (remaining <= 0)
                {
                    break;
                }

                values.Add(Value(item, depth + 1, ref remaining));
            }
            return values;
        }
        if (value is StructureValue structure)
        {
            Dictionary<string, object?> values = new(StringComparer.Ordinal);
            foreach (LogEventProperty item in structure.Properties.Take(32))
            {
                if (remaining <= 0)
                {
                    break;
                }

                values[item.Name] = Value(item.Value, depth + 1, ref remaining);
            }
            return values;
        }
        return "[redacted]";
    }
}
