using System.Diagnostics;
using OpenTelemetry;

namespace Wallow.ServiceDefaults;

internal sealed class IndependentTraceExporter(IndependentTelemetry telemetry) : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (Activity activity in batch)
        {
            telemetry.WriteSpan(activity);
        }

        return ExportResult.Success;
    }
}
