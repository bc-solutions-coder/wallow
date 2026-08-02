using Wallow.SeederService;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Load seed.json: prefer SEED_FILE_PATH env var, fall back to bundled file
string seedFilePath = Environment.GetEnvironmentVariable("SEED_FILE_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "seed.json");

builder.Configuration.AddJsonFile(seedFilePath, optional: false, reloadOnChange: false);

// Load environment-specific overrides (e.g. seed.Development.json)
string seedDir = Path.GetDirectoryName(seedFilePath) ?? AppContext.BaseDirectory;
string seedEnvPath = Path.Combine(seedDir, $"seed.{builder.Environment.EnvironmentName}.json");
builder.Configuration.AddJsonFile(seedEnvPath, optional: true, reloadOnChange: false);

// Re-add environment variables so they take precedence over seed.json values.
// Host.CreateApplicationBuilder adds env vars early, but seed.json (added above) overrides them.
// This ensures Docker/CI overrides like Clients__1__RedirectUris__0 are applied.
builder.Configuration.AddEnvironmentVariables();

// Bind SeedOptions from config root
builder.Services.Configure<SeedOptions>(builder.Configuration);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddSeederIdentityServices(builder.Configuration, connectionString);

builder.Services.AddHostedService<SeederWorker>();

IHost host = builder.Build();
await host.RunAsync();
