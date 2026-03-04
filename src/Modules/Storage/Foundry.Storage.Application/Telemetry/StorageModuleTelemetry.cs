using System.Diagnostics;
using System.Diagnostics.Metrics;
using Foundry.Shared.Kernel;

namespace Foundry.Storage.Application.Telemetry;

public static class StorageModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Storage");
    public static readonly Meter Meter = Diagnostics.CreateMeter("Storage");

    public static readonly Counter<long> OperationsTotal =
        Meter.CreateCounter<long>(
            "foundry.storage.operations_total",
            description: "Total number of storage operations");

    public static readonly Counter<long> BytesTransferred =
        Meter.CreateCounter<long>(
            "foundry.storage.bytes_transferred",
            unit: "bytes",
            description: "Total bytes transferred");

    public static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>(
            "foundry.storage.operation_duration",
            unit: "ms",
            description: "Duration of storage operations in milliseconds");

    public static Activity? StartUploadActivity() =>
        ActivitySource.StartActivity("Storage.Upload");

    public static Activity? StartDownloadActivity() =>
        ActivitySource.StartActivity("Storage.Download");

    public static Activity? StartDeleteActivity() =>
        ActivitySource.StartActivity("Storage.Delete");
}
