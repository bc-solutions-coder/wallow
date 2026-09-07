using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Wallow.TelemetryGateway;

public sealed class GatewayHealth
{
    private readonly long[] _responses = new long[600];
    private long _active;
    private long _bytes;

    public void Observe(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            Interlocked.Increment(ref _active);
            bool failed = false;
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception)
            {
                failed = true;
                throw;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
                int status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
                if (status is >= 100 and < 600) { Interlocked.Increment(ref _responses[status]); }
                Interlocked.Add(ref _bytes, context.Request.ContentLength ?? 0);
            }
        });
    }

    public WebApplication Create(string dataPath)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication app = builder.Build();
        app.MapGet("/metrics", () =>
        {
            StringBuilder result = new();
            for (int status = 100; status < _responses.Length; status++)
            {
                long count = Interlocked.Read(ref _responses[status]);
                if (count > 0) { result.Append(CultureInfo.InvariantCulture, $"wallow_gateway_requests_total{{status=\"{status}\"}} {count}\n"); }
            }
            using Process process = Process.GetCurrentProcess();
            result.Append(CultureInfo.InvariantCulture, $"wallow_gateway_active_requests {Interlocked.Read(ref _active)}\nwallow_gateway_declared_bytes_total {Interlocked.Read(ref _bytes)}\nwallow_gateway_working_set_bytes {process.WorkingSet64}\nwallow_gateway_memory_limit_bytes {384 * 1024 * 1024}\n");
            DriveInfo drive = new(Path.GetDirectoryName(Path.GetFullPath(dataPath))!);
            result.Append(CultureInfo.InvariantCulture, $"wallow_storage_filesystem_size_bytes {drive.TotalSize}\nwallow_storage_filesystem_available_bytes {drive.AvailableFreeSpace}\n");
            return Results.Text(result.ToString(), "text/plain; version=0.0.4");
        });
        return app;
    }
}
