IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
IResourceBuilder<PostgresDatabaseResource> postgres = builder.AddPostgres("postgres").AddDatabase("wallow");
IResourceBuilder<RedisResource> valkey = builder.AddRedis("valkey");

// S3-compatible object storage
builder.AddContainer("garage", "dxflrs/garage", "v1.1.0")
    .WithEndpoint(3900, 3900, name: "s3")
    .ExcludeFromManifest();

// Development email server
builder.AddContainer("mailpit", "axllent/mailpit", "v1.22")
    .WithEndpoint(8025, 8025, name: "http")
    .WithEndpoint(1025, 1025, name: "smtp");

// Antivirus scanning (optional)
builder.AddContainer("clamav", "clamav/clamav", "1.5.2")
    .WithEndpoint(3310, 3310, name: "clamd")
    .ExcludeFromManifest();

// Migrations run first
IResourceBuilder<ProjectResource> migrations = builder.AddProject<Projects.Wallow_MigrationService>("wallow-migrations")
    .WithReference(postgres);

// Application services
builder.AddProject<Projects.Wallow_Api>("wallow-api")
    .WithReference(postgres)
    .WithReference(valkey)
    .WaitFor(migrations);

builder.AddProject<Projects.Wallow_Auth>("wallow-auth")
    .WithReference(valkey)
    .WaitFor(migrations);

builder.AddProject<Projects.Wallow_Web>("wallow-web")
    .WaitFor(migrations);

await builder.Build().RunAsync();
