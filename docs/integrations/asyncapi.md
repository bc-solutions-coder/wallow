# AsyncAPI Event Catalog

Wallow auto-generates an [AsyncAPI 3.0](https://www.asyncapi.com/) event catalog from the codebase using reflection. The catalog is always up-to-date — no manual YAML or JSON files to maintain.

---

## Accessing the Event Catalog

Three dev-only endpoints are available when running in the `Development` environment:

| Endpoint | Content-Type | Description |
|----------|--------------|-------------|
| `http://localhost:5000/asyncapi` | `text/html` | Interactive viewer (AsyncAPI React component) |
| `http://localhost:5000/asyncapi/v1.json` | `application/json` | Raw AsyncAPI 3.0 JSON specification |
| `http://localhost:5000/asyncapi/v1/flows` | `text/plain` | Mermaid diagram of event flows between modules |

These endpoints are excluded from OpenAPI/Scalar documentation and are not available in staging or production.

---

## Interactive Viewer

Navigate to `http://localhost:5000/asyncapi` in your browser. The viewer renders the full AsyncAPI spec using the official `@asyncapi/react-component`, showing:

- All integration events grouped by module (channel)
- Event payload schemas with property types
- Publisher and subscriber information

---

## Event Flow Diagrams

The `/asyncapi/v1/flows` endpoint returns a Mermaid flowchart showing which modules publish and consume each event. Paste the output into any Mermaid renderer (GitHub markdown, Mermaid Live Editor, IDE plugins) to visualize the event flow.

Example usage:

```bash
curl http://localhost:5000/asyncapi/v1/flows
```

The output is a `flowchart LR` diagram with modules as nodes and events as labeled edges.

---

## JSON Specification

The `/asyncapi/v1.json` endpoint returns the full AsyncAPI 3.0 document. Use it for:

- **Code generation** — generate client SDKs or typed consumers from the spec
- **Tooling integration** — import into AsyncAPI Studio, Postman, or other API tools
- **CI validation** — diff the spec between commits to detect breaking event changes

```bash
curl http://localhost:5000/asyncapi/v1.json | jq .
```

---

## How It Works

The catalog is generated at startup by scanning all `Wallow.*` assemblies:

1. **Event discovery** — `EventFlowDiscovery` finds all classes implementing `IIntegrationEvent` in `Wallow.Shared.Contracts` namespaces. The namespace determines the module (e.g., `Wallow.Shared.Contracts.Storage.Events` → Storage).

2. **Consumer discovery** — Wolverine handler classes with `Handle`/`HandleAsync` methods accepting an `IIntegrationEvent` parameter are identified as consumers. The handler's assembly determines the consuming module.

3. **Schema generation** — `JsonSchemaGenerator` converts event C# types to JSON Schema for the payload definitions.

4. **Document generation** — `AsyncApiDocumentGenerator` assembles the AsyncAPI 3.0 document with channels, operations, and message schemas.

5. **Flow generation** — `MermaidFlowGenerator` produces a Mermaid flowchart from the discovered publish/subscribe relationships.

Key source files:

| File | Purpose |
|------|---------|
| `api/src/Wallow.Api/Extensions/AsyncApiEndpointExtensions.cs` | Endpoint registration (dev only) |
| `api/src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/EventFlowDiscovery.cs` | Reflection-based event and consumer discovery |
| `api/src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/AsyncApiDocumentGenerator.cs` | AsyncAPI 3.0 JSON document builder |
| `api/src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/MermaidFlowGenerator.cs` | Mermaid diagram generator |
| `api/src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/JsonSchemaGenerator.cs` | C# type to JSON Schema converter |

---

## Adding New Events

Nothing special is required. When you add a new integration event to `Wallow.Shared.Contracts` and a corresponding Wolverine handler, the catalog picks them up automatically on the next app restart.

Follow the existing conventions to ensure clean discovery:

- Place events in `Wallow.Shared.Contracts.{Module}.Events` namespace
- Inherit from `IntegrationEvent` (or implement `IIntegrationEvent`)
- Use past-tense naming: `InvoicePaidEvent`, not `PayInvoiceEvent`
- Use primitive types (`Guid`, `string`, `decimal`) — not domain value objects

See the [Messaging Guide](../architecture/messaging.md#integration-events) for full event authoring details.

---

## Troubleshooting

**Endpoints return 404**
The catalog is only available in the `Development` environment. Check that `ASPNETCORE_ENVIRONMENT=Development` is set (this is the default when using `dotnet run`).

**An event is missing from the catalog**
- Verify the event class is in a `Wallow.Shared.Contracts.*.Events` namespace
- Verify it implements `IIntegrationEvent` (or inherits `IntegrationEvent`)
- Restart the API — discovery runs at startup, not at runtime

**A consumer is missing**
- Verify the handler follows Wolverine conventions (`Handle`/`HandleAsync` method)
- Verify the handler is in a `Wallow.*` assembly (included in assembly scanning)
- Verify the handler class is `public`

**Viewer shows a blank page**
The interactive viewer loads the AsyncAPI React component from unpkg.com CDN. Ensure you have internet access, or check browser console for network errors.
