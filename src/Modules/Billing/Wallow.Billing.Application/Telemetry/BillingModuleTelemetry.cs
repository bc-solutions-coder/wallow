using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

namespace Wallow.Billing.Application.Telemetry;

public static class BillingModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Billing");
    public static readonly Meter Meter = Diagnostics.CreateMeter("Billing");

    public static readonly Counter<long> InvoicesCreatedTotal =
        Meter.CreateCounter<long>("wallow.billing.invoices_created_total");

    public static readonly Histogram<double> InvoiceAmount =
        Meter.CreateHistogram<double>("wallow.billing.invoice_amount");
}
