using Wallow.Shared.Kernel.Settings;

namespace Wallow.Billing.Application.Settings;

public class BillingSettingKeys : SettingRegistryBase
{
    public override string ModuleName => "billing";

    public static readonly SettingDefinition<string> DefaultCurrency = new(
        Key: "billing.default_currency",
        DefaultValue: "USD",
        Description: "The default currency code used for new invoices and charges");

    public static readonly SettingDefinition<string> InvoicePrefix = new(
        Key: "billing.invoice_prefix",
        DefaultValue: "INV-",
        Description: "Prefix applied to generated invoice numbers");

    public static readonly SettingDefinition<string> DateFormat = new(
        Key: "billing.date_format",
        DefaultValue: "YYYY-MM-DD",
        Description: "Date format used in billing documents and exports");

    public static readonly SettingDefinition<int> PaymentRetryAttempts = new(
        Key: "billing.payment_retry_attempts",
        DefaultValue: 3,
        Description: "Number of automatic retry attempts for failed payments");
}
