using System.Net;
using System.Text.Json;
using Wallow.TelemetryGateway;

if (Environment.GetEnvironmentVariable("GATEWAY_ROLE") == "operator")
{
    await using WebApplication boundary = OperatorBoundary.Create(
        new Uri("http://grafana:3000"),
        IPAddress.Parse(Environment.GetEnvironmentVariable("OPERATOR_PROXY_PEER") ?? throw new InvalidOperationException("Missing verified peer")),
        Environment.GetEnvironmentVariable("OPERATOR_IDENTITY_HEADER") ?? throw new InvalidOperationException("Missing verified identity header"),
        JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync("/etc/observability/operators.json"))!);
    boundary.Urls.Clear();
    boundary.Urls.Add("http://0.0.0.0:8082");
    await boundary.RunAsync();
    return;
}

string database = Environment.GetEnvironmentVariable("GATEWAY_DATABASE") ?? "/var/lib/gateway/registry.db";
string secret = Environment.GetEnvironmentVariable("GATEWAY_MANAGEMENT_SECRET")
    ?? throw new InvalidOperationException("GATEWAY_MANAGEMENT_SECRET is required.");
string collectorSecret = Environment.GetEnvironmentVariable("GATEWAY_COLLECTOR_SECRET")
    ?? throw new InvalidOperationException("GATEWAY_COLLECTOR_SECRET is required.");
await using WebApplication control = await GatewayControl.CreateAsync(database, secret, TimeProvider.System);
control.Urls.Clear();
control.Urls.Add("http://0.0.0.0:8081");
GatewayHealth health = new();
await using WebApplication ingress = GatewayIngress.Create(database, new Uri("http://alloy:4318"), TimeProvider.System, new Uri("http://alloy:12347/collect"), collectorSecret, health);
ingress.Urls.Clear();
ingress.Urls.Add("http://0.0.0.0:8080");
await using WebApplication metrics = health.Create(database);
metrics.Urls.Clear();
metrics.Urls.Add("http://0.0.0.0:8083");
await Task.WhenAll(control.RunAsync(), ingress.RunAsync(), metrics.RunAsync());
