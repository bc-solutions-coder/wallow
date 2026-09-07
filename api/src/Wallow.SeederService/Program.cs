using Wallow.SeederService;
using Wallow.ServiceDefaults;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


string seedFilePath = Environment.GetEnvironmentVariable("SEED_FILE_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "seed.json");

builder.Configuration.AddJsonFile(seedFilePath, optional: false, reloadOnChange: false);


string seedDir = Path.GetDirectoryName(seedFilePath) ?? AppContext.BaseDirectory;
string seedEnvPath = Path.Combine(seedDir, $"seed.{builder.Environment.EnvironmentName}.json");
builder.Configuration.AddJsonFile(seedEnvPath, optional: true, reloadOnChange: false);

// Environment variables override both seed files.
builder.Configuration.AddEnvironmentVariables();


builder.Services.Configure<SeedOptions>(builder.Configuration);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddSeederIdentityServices(builder.Configuration, connectionString);

builder.Services.AddSingleton<WorkerRunOutcome>();
builder.Services.AddHostedService<SeederWorker>();

IHost host = builder.Build();

// Retain the outcome before RunAsync disposes the service provider.
WorkerRunOutcome outcome = host.Services.GetRequiredService<WorkerRunOutcome>();

await host.RunAsync();

return outcome.ExitCode;
