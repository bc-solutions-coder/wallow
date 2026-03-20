using Serilog.Core;
using Serilog.Events;

namespace Wallow.Api.Logging;

/// <summary>
/// Enriches log events with a Module property extracted from the SourceContext namespace.
/// Assumes namespaces follow the pattern Wallow.{ModuleName}.*.
/// </summary>
internal class ModuleEnricher : ILogEventEnricher
{
    private const string DefaultModule = "System";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        string module = DefaultModule;

        if (logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? sourceContext))
        {
            string contextValue = sourceContext.ToString().Trim('"');
            string[] parts = contextValue.Split('.');

            // Wallow.{X}.* → Module = X
            if (parts.Length >= 2 && parts[0] == "Wallow")
            {
                module = parts[1];
            }
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Module", module));
    }
}
