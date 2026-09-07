using Wallow.TelemetryGateway;

string database = Environment.GetEnvironmentVariable("GATEWAY_DATABASE") ?? "/var/lib/gateway/registry.db";
string secret = Environment.GetEnvironmentVariable("GATEWAY_MANAGEMENT_SECRET")
    ?? throw new InvalidOperationException("GATEWAY_MANAGEMENT_SECRET is required.");
string collectorSecret = Environment.GetEnvironmentVariable("GATEWAY_COLLECTOR_SECRET")
    ?? throw new InvalidOperationException("GATEWAY_COLLECTOR_SECRET is required.");
await using WebApplication control = await GatewayControl.CreateAsync(database, secret, TimeProvider.System);
control.Urls.Clear();
control.Urls.Add("http://0.0.0.0:8081");
await using WebApplication ingress = GatewayIngress.Create(database, new Uri("http://alloy:4318"), TimeProvider.System, new Uri("http://alloy:12347/collect"), collectorSecret);
ingress.Urls.Clear();
ingress.Urls.Add("http://0.0.0.0:8080");
await Task.WhenAll(control.RunAsync(), ingress.RunAsync());
