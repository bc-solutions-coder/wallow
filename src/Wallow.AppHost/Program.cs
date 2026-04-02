IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Resolve paths relative to the AppHost project
string garageConfigDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "docker", "garage"));

// Infrastructure
IResourceBuilder<PostgresDatabaseResource> postgres = builder.AddPostgres("postgres")
    .AddDatabase("wallow");
IResourceBuilder<RedisResource> valkey = builder.AddRedis("valkey");

// S3-compatible object storage (built from docker/garage/Dockerfile: Alpine + garage binary + init script)
// Credentials must match appsettings.Development.json so the API can authenticate
IResourceBuilder<ContainerResource> garage = builder.AddDockerfile("garage", garageConfigDir)
    .WithHttpEndpoint(targetPort: 3900, name: "s3")
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

// API waits for all infrastructure + migrations
IResourceBuilder<ProjectResource> api = builder.AddProject<Projects.Wallow_Api>("wallow-api")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WithReference(valkey, connectionName: "Redis")
    .WithEnvironment("Storage__S3__Endpoint", garage.GetEndpoint("s3"))
    .WaitForCompletion(migrations)
    .WaitFor(valkey)
    .WaitFor(garage);

// Auth and Web wait for API to be fully ready
builder.AddProject<Projects.Wallow_Auth>("wallow-auth")
    .WithReference(valkey, connectionName: "Redis")
    .WaitFor(api)
    .WaitFor(valkey);

builder.AddProject<Projects.Wallow_Web>("wallow-web")
    .WithReference(valkey, connectionName: "Redis")
    .WaitFor(api)
    .WaitFor(valkey);

await builder.Build().RunAsync();
