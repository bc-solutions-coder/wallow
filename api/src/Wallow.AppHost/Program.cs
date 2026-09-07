IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);


string garageImageDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "docker", "images", "garage"));
string wallowAuthDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "apps", "wallow-auth"));
string wallowWebDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "apps", "wallow-web"));


IResourceBuilder<PostgresDatabaseResource> postgres = builder.AddPostgres("postgres")
    .AddDatabase("wallow");
// Use plain Redis locally so Node clients need no Aspire developer-certificate trust.
IResourceBuilder<RedisResource> valkey = builder.AddRedis("valkey")
    .WithoutHttpsCertificate();

// Garage credentials must match the API development storage configuration.
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


builder.AddContainer("mailpit", "axllent/mailpit", "v1.22")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(1025, 1025, name: "smtp");

// Start ClamAV locally but omit it from the published manifest.
builder.AddContainer("clamav", "clamav/clamav", "1.5.2")
    .WithEndpoint(3310, 3310, name: "clamd")
    .ExcludeFromManifest();


IResourceBuilder<ProjectResource> migrations = builder.AddProject<Projects.Wallow_MigrationService>("wallow-migrations")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WaitFor(postgres);


IResourceBuilder<ProjectResource> seeder = builder.AddProject<Projects.Wallow_SeederService>("wallow-seeder")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WaitForCompletion(migrations);

// Start the API after seeding, Redis, and Garage are ready.
IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.Wallow_Api>("wallow-api")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WithReference(valkey, connectionName: "Redis")
    .WithEnvironment("Storage__S3__Endpoint", garage.GetEndpoint("s3"))
    .WaitForCompletion(seeder)
    .WaitFor(valkey)
    .WaitFor(garage);

// Resolve the API address through Aspire endpoint discovery.
EndpointReference apiEndpoint = api.GetEndpoint("http");

// Node log ingestion sends OTLP/JSON to the dev infrastructure Alloy HTTP receiver.
// This external collector is supplied by pnpm backend:infra, not this AppHost.
const string otlpHttpEndpoint = "http://localhost:4318";
const string otlpHttpProtocol = "http/json";

// Node proxy configuration consumes WALLOW_API_INTERNAL_URL, not Aspire service-discovery variables.
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

// Match client credentials and callback URLs in the seed configuration.
builder.AddJavaScriptApp("wallow-web", wallowWebDir, "dev")
    .WithPnpm()
    .WithHttpEndpoint(port: 3000, env: "PORT", isProxied: false)
    .WithReference(valkey, connectionName: "Redis")
    .WithReference(api)
    // Expect the public auth issuer while fetching discovery directly from the API.
    .WithEnvironment("OIDC_ISSUER", "http://localhost:3002")
    .WithEnvironment("OIDC_METADATA_URL", ReferenceExpression.Create($"{apiEndpoint}/.well-known/openid-configuration"))
    .WithEnvironment("OIDC_CLIENT_ID", "wallow-web-client")
    .WithEnvironment("OIDC_CLIENT_SECRET", "wallow-web-secret")
    .WithEnvironment("OIDC_REDIRECT_URI", "http://localhost:3000/bff/callback")
    .WithEnvironment("OIDC_POST_LOGOUT_REDIRECT_URI", "http://localhost:3000")
    .WithEnvironment("BFF_API_BASE_URL", apiEndpoint)
    .WithEnvironment("COOKIE_PASSWORD", "wallow-web-dev-cookie-seal-password-min-32-chars")
    // Local HTTP development requires cookies without the Secure flag.
    .WithEnvironment("COOKIE_SECURE", "false")
    // The BFF selects its Redis session store through REDIS_URL.
    .WithEnvironment("REDIS_URL", valkey.Resource.UriExpression)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpHttpEndpoint)
    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpHttpProtocol)
    .WaitFor(api)
    .WaitFor(valkey);

await builder.Build().RunAsync();
