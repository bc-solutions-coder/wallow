using System.Reflection;
using System.Text.Json.Nodes;

namespace Foundry.Shared.Infrastructure.AsyncApi;

public sealed class AsyncApiDocumentGenerator
{
    private readonly EventFlowInfo[] _flows;
    private JsonObject? _cached;

    public AsyncApiDocumentGenerator(EventFlowInfo[] flows)
    {
        _flows = flows;
    }

    public JsonObject GenerateDocument()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        JsonObject doc = new JsonObject
        {
            ["asyncapi"] = "3.0.0",
            ["info"] = BuildInfo(),
            ["channels"] = BuildChannels(),
            ["operations"] = BuildOperations(),
            ["components"] = new JsonObject
            {
                ["schemas"] = BuildSchemas()
            }
        };

        _cached = doc;
        return doc;
    }

    private static JsonObject BuildInfo()
    {
        string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

        return new JsonObject
        {
            ["title"] = "Foundry Event Catalog",
            ["version"] = version
        };
    }

    private JsonObject BuildChannels()
    {
        JsonObject channels = new JsonObject();

        foreach (EventFlowInfo flow in _flows)
        {
            channels[flow.EventTypeName] = new JsonObject
            {
                ["address"] = flow.ExchangeName,
                ["messages"] = new JsonObject
                {
                    [flow.EventTypeName] = new JsonObject
                    {
                        ["$ref"] = $"#/components/schemas/{flow.EventTypeName}"
                    }
                },
                ["bindings"] = new JsonObject
                {
                    ["amqp"] = new JsonObject
                    {
                        ["is"] = "routingKey",
                        ["exchange"] = new JsonObject
                        {
                            ["name"] = flow.ExchangeName,
                            ["type"] = "fanout"
                        }
                    }
                }
            };
        }

        return channels;
    }

    private JsonObject BuildOperations()
    {
        JsonObject operations = new JsonObject();

        foreach (EventFlowInfo flow in _flows)
        {
            // Send operation from the producing module
            operations[$"{flow.SourceModule}.publish.{flow.EventTypeName}"] = new JsonObject
            {
                ["action"] = "send",
                ["channel"] = new JsonObject
                {
                    ["$ref"] = $"#/channels/{flow.EventTypeName}"
                },
                ["summary"] = $"{flow.SourceModule} publishes {flow.EventTypeName}"
            };

            // Receive operation per consumer
            foreach (ConsumerInfo consumer in flow.Consumers)
            {
                string opId = consumer.IsSaga
                    ? $"{consumer.Module}.saga.{flow.EventTypeName}"
                    : $"{consumer.Module}.subscribe.{flow.EventTypeName}";

                // Avoid duplicate keys if multiple handlers in same module
                if (operations.ContainsKey(opId))
                {
                    opId = $"{opId}.{consumer.HandlerTypeName}";
                }

                operations[opId] = new JsonObject
                {
                    ["action"] = "receive",
                    ["channel"] = new JsonObject
                    {
                        ["$ref"] = $"#/channels/{flow.EventTypeName}"
                    },
                    ["summary"] = $"{consumer.Module} consumes {flow.EventTypeName} via {consumer.HandlerTypeName}.{consumer.HandlerMethodName}"
                };
            }
        }

        return operations;
    }

    private JsonObject BuildSchemas()
    {
        JsonObject schemas = new JsonObject();

        foreach (EventFlowInfo flow in _flows)
        {
            schemas[flow.EventTypeName] = JsonSchemaGenerator.GenerateSchema(flow.EventType);
        }

        return schemas;
    }
}
