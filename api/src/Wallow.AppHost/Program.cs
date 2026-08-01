IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve paths relative to the AppHost project
string garageImageDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "docker", "images", "garage"));
string wallowAuthDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "apps", "wallow-auth"));
string wallowWebDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "apps", "wallow-web"));

// Infrastructure
IResourceBuilder<PostgresDatabaseResource> postgres = builder.AddPostgres("postgres")
    .AddDatabase("wallow");
// Aspire terminates the Redis endpoint with TLS and its own developer certificate by default.
// It configures certificate trust for .NET and container resources, but not for JavaScript app
// resources (it never sets NODE_EXTRA_CA_CERTS), so node-redis in wallow-web cannot verify that
// certificate and a rediss:// URL fails with "self-signed certificate". Plain TCP here is what
// both compose stacks already run.
IResourceBuilder<RedisResource> valkey = builder.AddRedis("valkey")
    .WithoutHttpsCertificate();

// S3-compatible object storage (built from docker/images/garage/Dockerfile: Alpine + garage
// binary + entrypoint that renders garage.toml from env). Credentials must match
// appsettings.Development.json so the API can authenticate.
IResourceBuilder<ContainerResource> garage = builder.AddDockerfile("garage", garageImageDir)
    .WithHttpEndpoint(targetPort: 3900, name: "s3")
    .WithEnvironment("GARAGE_RPC_SECRET", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")
    .WithEnvironment("GARAGE_ADMIN_TOKEN", "wallow-admin-token")
    .WithEnvironment("GARAGE_KEY_NAME", "wallow-dev")
    .WithEnvironment("GARAGE_ACCESS_KEY", "GKac08a4bd9e083da18a8619d6")
    .WithEnvironment("GARAGE_SECRET_KEY", "40b1e64b357741d678d0f1ed77ec332e0f38cd59724d45a904d8ffd5dfeea943")
    .WithEnvironment("GARAGE_BUCKET", "wallow-files")
    .WithVolume("garage-meta", "/var/lib/garage/meta")
    .WithVolume("garage-data", "/var/lib/garage/data");

// Development email server
builder.AddContainer("mailpit", "axllent/mailpit", "v1.22")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(1025, 1025, name: "smtp");

// Antivirus scanning (optional)
builder.AddContainer("clamav", "clamav/clamav", "1.5.2")
    .WithEndpoint(3310, 3310, name: "clamd")
    .ExcludeFromManifest();

// Migrations run after infrastructure is ready, then exit
IResourceBuilder<ProjectResource> migrations = builder.AddProject<Projects.Wallow_MigrationService>("wallow-migrations")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WaitFor(postgres);

// Seeder runs after migrations, seeds roles/scopes/admin/clients from seed.json, then exits
IResourceBuilder<ProjectResource> seeder = builder.AddProject<Projects.Wallow_SeederService>("wallow-seeder")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WaitForCompletion(migrations);

// API waits for all infrastructure + migrations
IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.Wallow_Api>("wallow-api")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WithReference(valkey, connectionName: "Redis")
    .WithEnvironment("Storage__S3__Endpoint", garage.GetEndpoint("s3"))
    .WaitForCompletion(seeder)
    .WaitFor(valkey)
    .WaitFor(garage);

// The one place the API's address is named. It resolves to the DCP proxy Aspire puts in front of
// the project (http://localhost:5001 locally, pinned by Wallow.Api's launchSettings applicationUrl
// while the process itself binds a dynamic port behind it), and to a per-environment binding
// placeholder in a published manifest. Spelling the URL out as a literal instead would duplicate a
// port launchSettings owns, and would bake localhost into every deployment target.
EndpointReference apiEndpoint = api.GetEndpoint("http");

// Where the Node apps' log-ingest routes forward their batches.
//
// Aspire already injects OTEL_EXPORTER_OTLP_ENDPOINT into every managed resource, pointing at the
// dashboard's OTLP endpoint (DOTNET_DASHBOARD_OTLP_ENDPOINT_URL in launchSettings, HTTPS gRPC).
// That value is wrong twice over for @bc-solutions-coder/logger: it POSTs OTLP/JSON over HTTP and
// has no gRPC transport, and Aspire configures no certificate trust for JavaScript apps (see the
// Valkey note above), so even the HTTPS handshake would fail. Both failures are silent — a valid
// batch answers 204 regardless of collector health — so the override has to be explicit. It is
// registered after AddJavaScriptApp, which is what makes it win over Aspire's own value.
//
// This is one of the literals the comment above allows: it must equal a value another file already
// fixed. Alloy is not an Aspire resource, so there is no endpoint to reference. It comes from the
// dev infrastructure stack (pnpm backend:infra), which publishes the OTLP HTTP receiver on
// 127.0.0.1:4318 (docker/docker-compose.yml). With that stack down the POST fails and the batch is
// dropped with a console warning, which is what Aspire's own value already did, only reachably.
//
// OTEL_EXPORTER_OTLP_PROTOCOL goes with it. The logger never reads it, but Aspire sets it to "grpc"
// beside the endpoint, and leaving "grpc" naming an HTTP port is the same trap this override exists
// to close: the next thing to read the pair would believe it.
const string otlpHttpEndpoint = "http://localhost:4318";
const string otlpHttpProtocol = "http/json";

// Auth and Web wait for API to be fully ready.
// Auth is the TanStack Start app (apps/wallow-auth), run via its pnpm dev script (vite dev) on port 3002.
// WithReference(api) injects Aspire service-discovery vars, which the Node host never reads, so
// WALLOW_API_INTERNAL_URL still has to name the upstream for the app's server-side API proxy
// outright (the proxy's own default, http://wallow-api, is Docker DNS and does not resolve here).
builder.AddJavaScriptApp("wallow-auth", wallowAuthDir, "dev")
    .WithPnpm()
    .WithHttpEndpoint(port: 3002, env: "PORT", isProxied: false)
    .WithReference(valkey, connectionName: "Redis")
    .WithReference(api)
    .WithEnvironment("WALLOW_API_INTERNAL_URL", apiEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpHttpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpHttpProtocol)
    .WaitFor(api)
    .WaitFor(valkey);

// Web is the TanStack Start app (apps/wallow-web), run via its pnpm dev script on port 3000.
// WithReference(api) injects Aspire service-discovery vars; the OIDC_*/BFF/COOKIE vars are the
// BFF config that loadBffConfigFromEnv() requires (it throws on any missing var). Values mirror
// docker/docker-compose.test.yml, remapped to the Aspire-local ports.
// wallow-web-client/wallow-web-secret are the seeded OIDC client credentials (api/seed.json).
//
// Only the two vars that name WHERE THE API LISTENS go through apiEndpoint. The rest stay literal
// on purpose: each must equal a value some other file already fixed — the redirect URIs are
// matched against the ones registered in api/seed.json, and OIDC_ISSUER against the issuer the API
// derives from AuthUrl — so pointing them at an Aspire endpoint would only make them agree with
// the app's own port by coincidence, and quietly disagree the moment that file changes.
builder.AddJavaScriptApp("wallow-web", wallowWebDir, "dev")
    .WithPnpm()
    .WithHttpEndpoint(port: 3000, env: "PORT", isProxied: false)
    .WithReference(valkey, connectionName: "Redis")
    .WithReference(api)
    // Issuer/metadata split (mirrors docker-compose.test.yml): the API's dev issuer is the
    // wallow-auth origin (appsettings.Development.json AuthUrl -> OpenIddictIssuerResolver),
    // so the client must expect that issuer, while discovery is fetched from the API directly
    // (the auth app's passthrough would serve it too, but going direct saves a proxy hop).
    .WithEnvironment("OIDC_ISSUER", "http://localhost:3002")
    .WithEnvironment("OIDC_METADATA_URL", ReferenceExpression.Create($"{apiEndpoint}/.well-known/openid-configuration"))
    .WithEnvironment("OIDC_CLIENT_ID", "wallow-web-client")
    .WithEnvironment("OIDC_CLIENT_SECRET", "wallow-web-secret")
    .WithEnvironment("OIDC_REDIRECT_URI", "http://localhost:3000/bff/callback")
    .WithEnvironment("OIDC_POST_LOGOUT_REDIRECT_URI", "http://localhost:3000")
    .WithEnvironment("BFF_API_BASE_URL", apiEndpoint)
    .WithEnvironment("COOKIE_PASSWORD", "wallow-web-dev-cookie-seal-password-min-32-chars")
    // Safari/WebKit refuses Secure cookies over plain-HTTP localhost (Chrome and Firefox
    // allow them), so the BFF's login-transaction cookie never survives the redirect and
    // every /bff/callback fails with a 400. Local dev is plain HTTP, so drop the flag here.
    .WithEnvironment("COOKIE_SECURE", "false")
    // The BFF picks the Valkey session store only when REDIS_URL is set. WithReference above
    // injects ConnectionStrings__Redis instead, which the Node host never reads, so without
    // this the store silently degrades to stateless cookie sessions here while both compose
    // stacks use Valkey. UriExpression renders redis://[:{password}@]{host}:{port}.
    .WithEnvironment("REDIS_URL", valkey.Resource.UriExpression)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpHttpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpHttpProtocol)
    .WaitFor(api)
    .WaitFor(valkey);

await builder.Build().RunAsync();
