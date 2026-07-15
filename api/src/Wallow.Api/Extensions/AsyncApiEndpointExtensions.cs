using System.Reflection;
using Wallow.Shared.Infrastructure.AsyncApi;

namespace Wallow.Api.Extensions;

internal static class AsyncApiEndpointExtensions
{
    public static WebApplication MapAsyncApiEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true)
            .ToArray();

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
