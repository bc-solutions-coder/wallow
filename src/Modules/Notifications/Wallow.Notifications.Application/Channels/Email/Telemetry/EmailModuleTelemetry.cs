using System.Diagnostics;
using Wallow.Shared.Kernel;

namespace Wallow.Notifications.Application.Channels.Email.Telemetry;

public static class EmailModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Notifications.Email");
}
