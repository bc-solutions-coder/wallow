using Serilog.Core;
using Serilog.Events;

namespace Wallow.Api.Logging;

/// <summary>
/// Enriches log events with a Module property extracted from the SourceContext namespace.
/// Assumes namespaces follow the pattern {NamespacePrefix}.{ModuleName}.*.
/// </summary>
internal class ModuleEnricher : ILogEventEnricher
{
    private const string DefaultModule = "System";
    private readonly string _namespacePrefix;

    public ModuleEnricher()
    {
        _namespacePrefix = "Wallow";
    }

    public ModuleEnricher(IConfiguration configuration)
    {
        _namespacePrefix = configuration["Logging:NamespacePrefix"] ?? "Wallow";
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        string module = DefaultModule;

        if (logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? sourceContext))
        {
            string contextValue = sourceContext.ToString().Trim('"');
            string[] parts = contextValue.Split('.');

            if (parts.Length >= 2 && parts[0] == _namespacePrefix)
            {
                module = parts[1];
            }
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Module", module));
    }
}
