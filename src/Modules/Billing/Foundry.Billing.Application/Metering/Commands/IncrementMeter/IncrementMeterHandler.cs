using Foundry.Billing.Application.Metering.Services;
using Microsoft.Extensions.Logging;

namespace Foundry.Billing.Application.Metering.Commands.IncrementMeter;

public sealed partial class IncrementMeterHandler(
    IMeteringService meteringService,
    ILogger<IncrementMeterHandler> logger)
{
    public async Task Handle(IncrementMeterCommand command)
    {
        try
        {
            await meteringService.IncrementAsync(command.MeterCode, command.Value);
        }
        catch (Exception ex)
        {
            LogIncrementFailed(logger, command.MeterCode, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to increment metering counter for {MeterCode}")]
    private static partial void LogIncrementFailed(ILogger logger, string meterCode, Exception ex);
}
