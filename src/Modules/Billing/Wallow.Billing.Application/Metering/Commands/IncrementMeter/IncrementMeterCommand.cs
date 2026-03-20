namespace Wallow.Billing.Application.Metering.Commands.IncrementMeter;

public sealed record IncrementMeterCommand(string MeterCode, decimal Value = 1);
