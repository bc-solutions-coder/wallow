IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve paths relative to the AppHost project
string garageImageDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "docker", "images", "garage"));
string wallowAuthDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "apps", "wallow-auth"));
string wallowWebDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "apps", "wallow-web"));

// Infrastructure
IResourceBuilder<PostgresDatabaseResource> postgres = builder.AddPostgres("postgres")
    .AddDatabase("wallow");
IResourceBuilder<RedisResource> valkey = builder.AddRedis("valkey");

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

// Auth and Web wait for API to be fully ready.
// Auth is the TanStack Start app (apps/wallow-auth), run via its pnpm dev script (vite dev) on port 3002.
// WithReference(api) injects Aspire service-discovery vars; WALLOW_API_INTERNAL_URL is the
// explicit upstream for the app's server-side API proxy (the Aspire/Docker DNS default does not
// resolve locally).
builder.AddJavaScriptApp("wallow-auth", wallowAuthDir, "dev")
    .WithPnpm()
    .WithHttpEndpoint(port: 3002, env: "PORT", isProxied: false)
    .WithReference(valkey, connectionName: "Redis")
    .WithReference(api)
    .WithEnvironment("WALLOW_API_INTERNAL_URL", "http://localhost:5001")
    .WaitFor(api)
    .WaitFor(valkey);

// Web is the TanStack Start app (apps/wallow-web), run via its pnpm dev script on port 3000.
// WithReference(api) injects Aspire service-discovery vars; the OIDC_*/BFF/COOKIE vars are the
// BFF config that loadBffConfigFromEnv() requires (it throws on any missing var). Values mirror
// docker/docker-compose.test.yml, remapped to the Aspire-local API port 5001 and web port 3000.
// wallow-web-client/wallow-web-secret are the seeded OIDC client credentials (api/seed.json).
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
    .WithEnvironment("OIDC_METADATA_URL", "http://localhost:5001/.well-known/openid-configuration")
    .WithEnvironment("OIDC_CLIENT_ID", "wallow-web-client")
    .WithEnvironment("OIDC_CLIENT_SECRET", "wallow-web-secret")
    .WithEnvironment("OIDC_REDIRECT_URI", "http://localhost:3000/bff/callback")
    .WithEnvironment("OIDC_POST_LOGOUT_REDIRECT_URI", "http://localhost:3000")
    .WithEnvironment("BFF_API_BASE_URL", "http://localhost:5001")
    .WithEnvironment("COOKIE_PASSWORD", "wallow-web-dev-cookie-seal-password-min-32-chars")
    // Safari/WebKit refuses Secure cookies over plain-HTTP localhost (Chrome and Firefox
    // allow them), so the BFF's login-transaction cookie never survives the redirect and
    // every /bff/callback fails with a 400. Local dev is plain HTTP, so drop the flag here.
    .WithEnvironment("COOKIE_SECURE", "false")
    .WaitFor(api)
    .WaitFor(valkey);

await builder.Build().RunAsync();
