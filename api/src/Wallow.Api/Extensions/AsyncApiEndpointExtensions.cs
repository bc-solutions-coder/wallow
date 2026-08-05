using System.Reflection;
using Wallow.Shared.Contracts;
using Wallow.Shared.Infrastructure.AsyncApi;

namespace Wallow.Api.Extensions;

internal static class AsyncApiEndpointExtensions
{
    /// <summary>
    /// Publishes the AsyncAPI document for the handlers in <paramref name="handlerAssemblies"/>.
    /// </summary>
    /// <remarks>
    /// This used to run its own <c>AppDomain.CurrentDomain.GetAssemblies()</c> scan, identical in
    /// shape to the one Wolverine's discovery used. The two did not merely see the same assemblies
    /// in a different order — they saw different assemblies. At Wolverine-config time (inside
    /// <c>builder.Host.UseWolverine</c>) no <c>.Api</c> assembly is loaded yet, because MVC's
    /// application-part discovery has not run; this method runs after <c>builder.Build()</c>, when
    /// they are. Taking the registry's assembly list makes the document describe the handlers the
    /// host actually runs.
    /// </remarks>
    public static WebApplication MapAsyncApiEndpoints(
        this WebApplication app,
        IEnumerable<Assembly> handlerAssemblies)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        // Integration event types live in Wallow.Shared.Contracts, which hosts no handlers and so is
        // no module's handler assembly. Without it EventFlowDiscovery finds no events to document.
        Assembly[] assemblies = [.. handlerAssemblies.Append(typeof(IIntegrationEvent).Assembly).Distinct()];

        EventFlowDiscovery discovery = new();
        EventFlowInfo[] flows = discovery.Discover(assemblies).ToArray();
        AsyncApiDocumentGenerator generator = new(flows);
        string mermaid = MermaidFlowGenerator.Generate(flows);

        app.MapGet("/asyncapi/v1.json", () => Results.Json(generator.GenerateDocument()))
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/asyncapi/v1/flows", () => Results.Text(mermaid, "text/plain"))
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/asyncapi", () => Results.Content(ViewerHtml, "text/html"))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private const string ViewerHtml =
        """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Wallow AsyncAPI</title>
            <link rel="stylesheet" href="https://unpkg.com/@asyncapi/react-component@3.0.2/styles/default.min.css" integrity="sha384-+kAXZlmkYbACsvDm+h2/qAphvw98RHOGObISB6ouInRvC2tvmBLwvgZVZQOtMndl" crossorigin="anonymous">
        </head>
        <body>
            <div id="asyncapi"></div>
            <script src="https://unpkg.com/@asyncapi/react-component@3.0.2/browser/standalone/index.js" integrity="sha384-qYnchRkiLeA3INQMui0zmEqOZzAdSM6DTME5EPknhPDJNfi5FkyRVoSKfswOT1K/" crossorigin="anonymous"></script>
            <script>
                fetch('/asyncapi/v1.json')
                    .then(r => r.json())
                    .then(schema => AsyncApiStandalone.render({ schema }, document.getElementById('asyncapi')));
            </script>
        </body>
        </html>
        """;
}
