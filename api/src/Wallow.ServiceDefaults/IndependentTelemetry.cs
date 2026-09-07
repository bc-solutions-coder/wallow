using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace Wallow.ServiceDefaults;

/// <summary>Independent, byte-bounded OTLP HTTP export. Network failures never enter application logging.</summary>
public sealed class IndependentTelemetry : BackgroundService
{
    private static readonly JsonSerializerOptions _wireJson = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private const int RequestBytes = 1024 * 1024;
    private const int BufferBytes = 8 * RequestBytes;
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly Queue<Envelope> _queue = new();
    private readonly ConditionalWeakTable<Exception, object> _exceptions = new();
    private readonly SemaphoreSlim _flush = new(1, 1);
    private readonly HttpClient _client;
    private readonly SocketsHttpHandler _handler;
    private long _queuedBytes;
    private long _exported;
    private long _dropped;
    private long _failed;
    private bool _stopping;

    public IndependentTelemetry(Uri endpoint, string credential, string environment, string release)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
            || string.IsNullOrWhiteSpace(credential) || credential.Length > 512 || credential.Any(char.IsWhiteSpace)
            || !TelemetryPrivacy.IsLabel(environment) || !TelemetryPrivacy.IsLabel(release))
        {
            throw new ArgumentException("Invalid independent telemetry configuration", nameof(endpoint));
        }
        _handler = new SocketsHttpHandler { AllowAutoRedirect = false, ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator() };
        _client = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = endpoint,
            Timeout = _budget,
            MaxResponseContentBufferSize = 64 * 1024,
        };
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        _client.DefaultRequestHeaders.Add("X-Wallow-Environment", environment);
        _client.DefaultRequestHeaders.Add("X-Wallow-Release", release);
    }

    public TelemetryExportStats Stats()
    {
        lock (_gate)
        {
            return new(_queuedBytes, _exported, _dropped, _failed);
        }
    }

    public void WriteLog(LogLevel level, string eventName, IReadOnlyDictionary<string, object?> attributes, Exception? error = null)
    {
        try { WriteLogCore(level, eventName, attributes, error); }
        catch (Exception) { ReportCaptureFailure(); }
    }

    public void ReportCaptureFailure()
    {
        lock (_gate) { _dropped++; }
    }

    private void WriteLogCore(LogLevel level, string eventName, IReadOnlyDictionary<string, object?> attributes, Exception? error)
    {
        Dictionary<string, object?> safe = TelemetryPrivacy.Attributes(error is null ? attributes : attributes.Take(30));
        if (error is not null)
        {
            lock (_gate)
            {
                if (_exceptions.TryGetValue(error, out _))
                {
                    return;
                }

                _exceptions.Add(error, new object());
            }
            safe["exception.type"] = TelemetryPrivacy.Name(error.GetType().Name, "Error");
            safe["exception.stacktrace"] = TelemetryPrivacy.Stack(error);
        }
        Activity? activity = Activity.Current;
        object log = new
        {
            timeUnixNano = Nano(DateTimeOffset.UtcNow),
            severityNumber = (int)level * 4 + 1,
            severityText = level.ToString().ToUpperInvariant(),
            body = new { stringValue = TelemetryPrivacy.Name(eventName, "application.log") },
            traceId = activity?.TraceId.ToHexString(),
            spanId = activity?.SpanId.ToHexString(),
            attributes = safe.Take(32).Select(pair => new { key = pair.Key, value = new { stringValue = pair.Value is string text ? text : JsonSerializer.Serialize(pair.Value) } }),
        };
        Enqueue("logs", new { resource = new { attributes = Array.Empty<object>() }, scopeLogs = new[] { new { scope = new { name = "wallow.api" }, logRecords = new[] { log } } } });
    }

    internal void WriteSpan(Activity activity)
    {
        try { WriteSpanCore(activity); }
        catch (Exception) { ReportCaptureFailure(); }
    }

    private void WriteSpanCore(Activity activity)
    {
        object span = new
        {
            traceId = activity.TraceId.ToHexString(),
            spanId = activity.SpanId.ToHexString(),
            parentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString(),
            name = TelemetryPrivacy.Name(activity.DisplayName, "http.request"),
            kind = (int)activity.Kind + 1,
            startTimeUnixNano = Nano(activity.StartTimeUtc),
            endTimeUnixNano = Nano(activity.StartTimeUtc + activity.Duration),
            attributes = TelemetryPrivacy.WireAttributes(activity.TagObjects),
            events = activity.Events.Take(32).Select(item => new
            {
                name = TelemetryPrivacy.Name(item.Name, "application.event"),
                timeUnixNano = Nano(item.Timestamp),
                attributes = TelemetryPrivacy.WireAttributes(item.Tags.Where(pair => !pair.Key.StartsWith("exception.", StringComparison.Ordinal))),
            }),
            status = new { code = activity.Status == ActivityStatusCode.Error ? 2 : 1 },
        };
        Enqueue("traces", new { resource = new { attributes = Array.Empty<object>() }, scopeSpans = new[] { new { scope = new { name = "wallow.api" }, spans = new[] { span } } } });
        if (activity.Kind == ActivityKind.Server)
        {
            object metric = new { name = "wallow_request_duration_milliseconds", unit = "ms", gauge = new { dataPoints = new[] { new { timeUnixNano = Nano(DateTimeOffset.UtcNow), asDouble = activity.Duration.TotalMilliseconds, attributes = TelemetryPrivacy.WireAttributes(activity.TagObjects.Where(pair => pair.Key is "http.route" or "http.request.method" or "http.response.status_code")) } } } };
            Enqueue("metrics", new { resource = new { attributes = Array.Empty<object>() }, scopeMetrics = new[] { new { scope = new { name = "wallow.api" }, metrics = new[] { metric } } } });
        }
    }

    private void Enqueue(string signal, object resource)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(resource, _wireJson);
        lock (_gate)
        {
            if (_stopping || bytes.Length + 65 > RequestBytes || _queuedBytes + bytes.Length + 64 > BufferBytes)
            {
                _dropped++;
                return;
            }
            _queue.Enqueue(new(signal, bytes));
            _queuedBytes += bytes.Length + 64;
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_budget);
        bool acquired = false;
        try
        {
            await _flush.WaitAsync(budget.Token).ConfigureAwait(false);
            acquired = true;
            while (!budget.IsCancellationRequested)
            {
                List<Envelope> batch = [];
                int size = 64;
                lock (_gate)
                {
                    if (_queue.Count == 0)
                    {
                        return;
                    }

                    string signal = _queue.Peek().Signal;
                    int pending = _queue.Count;
                    for (int index = 0; index < pending; index++)
                    {
                        Envelope next = _queue.Dequeue();
                        if (next.Signal == signal && size + next.Bytes.Length + 1 <= RequestBytes)
                        {
                            batch.Add(next);
                            size += next.Bytes.Length + 1;
                        }
                        else { _queue.Enqueue(next); }
                    }
                }
                if (batch.Count == 0)
                {
                    lock (_gate)
                    {
                        Envelope rejected = _queue.Dequeue();
                        _queuedBytes -= rejected.Bytes.Length + 64;
                        _dropped++;
                    }
                    continue;
                }
                long held = batch.Sum(item => (long)item.Bytes.Length + 64);
                try
                {
                    using OtlpBatchContent content = new(batch[0].Signal, batch.Select(item => item.Bytes).ToArray());
                    using IDisposable suppression = SuppressInstrumentationScope.Begin();
                    using HttpResponseMessage response = await _client.PostAsync($"/v1/{batch[0].Signal}", content, budget.Token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            _exported += batch.Count;
                        }
                        else
                        {
                            _failed++;
                            _dropped += batch.Count;
                        }
                    }
                }
                catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
                {
                    lock (_gate)
                    {
                        _failed++;
                        _dropped += batch.Count;
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _queuedBytes -= held;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (acquired)
            {
                lock (_gate)
                {
                    if (budget.IsCancellationRequested)
                    {
                        _dropped += _queue.Count;
                        _queue.Clear();
                        _queuedBytes = 0;
                    }
                }
                _flush.Release();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_budget);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_budget);
        lock (_gate) { _stopping = true; }
        try
        {
            await base.StopAsync(budget.Token).ConfigureAwait(false);
            await FlushAsync(budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested) { }
        finally
        {
            lock (_gate)
            {
                _dropped += _queue.Count;
                _queuedBytes -= _queue.Sum(item => (long)item.Bytes.Length + 64);
                _queue.Clear();
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _client.Dispose();
        _handler.Dispose();
        _flush.Dispose();
    }

    private static string Nano(DateTimeOffset time) => ((time.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private sealed record Envelope(string Signal, byte[] Bytes);
}

public sealed record TelemetryExportStats(long QueuedBytes, long Exported, long Dropped, long Failed);
