using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Wallow.ServiceDefaults;

/// <summary>Streams queued records without allocating a second full request buffer.</summary>
internal sealed class OtlpBatchContent : HttpContent
{
    private readonly byte[] _prefix;
    private readonly byte[][] _records;
    private static readonly byte[] _separator = ","u8.ToArray();
    private static readonly byte[] _suffix = "]}"u8.ToArray();

    public OtlpBatchContent(string signal, byte[][] records)
    {
        string resource = signal switch { "logs" => "resourceLogs", "traces" => "resourceSpans", _ => "resourceMetrics" };
        _prefix = Encoding.UTF8.GetBytes($"{{\"{resource}\":[");
        _records = records;
        Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _prefix.Length + _suffix.Length + _records.Sum(record => (long)record.Length) + Math.Max(0, _records.Length - 1);
        return true;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(_prefix, cancellationToken).ConfigureAwait(false);
        for (int index = 0; index < _records.Length; index++)
        {
            if (index > 0)
            {
                await stream.WriteAsync(_separator, cancellationToken).ConfigureAwait(false);
            }
            await stream.WriteAsync(_records[index], cancellationToken).ConfigureAwait(false);
        }
        await stream.WriteAsync(_suffix, cancellationToken).ConfigureAwait(false);
    }
}
