using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Wallow.Api.Logging;

/// <summary>
/// Redacts configured public property names while destructuring objects.
/// This does not redact scalar strings or arbitrary text messages.
/// </summary>
internal class PiiDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> _sensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email",
        "Password",
        "PhoneNumber",
        "Token",
        "Secret",
        "CreditCard",
        "Ssn",
        "AccessToken",
        "RefreshToken",
    };

    private const string Redacted = "[REDACTED]";

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        Type type = value.GetType();


        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type.IsEnum)
        {
            result = null!;
            return false;
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length == 0)
        {
            result = null!;
            return false;
        }

        bool hasSensitive = false;
        foreach (PropertyInfo prop in properties)
        {
            if (_sensitivePropertyNames.Contains(prop.Name))
            {
                hasSensitive = true;
                break;
            }
        }

        if (!hasSensitive)
        {
            result = null!;
            return false;
        }

        List<LogEventProperty> sanitizedProperties = new(properties.Length);

        foreach (PropertyInfo prop in properties)
        {
            if (_sensitivePropertyNames.Contains(prop.Name))
            {
                sanitizedProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(Redacted)));
            }
            else
            {
                object? propValue;
                try
                {
                    propValue = prop.GetValue(value);
                }
                catch
                {
                    propValue = "Error reading property";
                }

                LogEventPropertyValue destructuredValue = propertyValueFactory.CreatePropertyValue(propValue, destructureObjects: true);
                sanitizedProperties.Add(new LogEventProperty(prop.Name, destructuredValue));
            }
        }

        result = new StructureValue(sanitizedProperties, type.Name);
        return true;
    }
}
