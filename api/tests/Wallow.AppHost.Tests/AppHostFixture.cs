using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Wallow.AppHost.Tests;

/// <summary>
/// Builds the Wallow Aspire AppHost application model once for the whole test class.
/// Resources are inspected in <see cref="DistributedApplicationOperation.Publish"/> mode so
/// declared environment variables and references resolve to manifest placeholders without
/// starting any containers.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    public IDistributedApplicationTestingBuilder Builder { get; private set; } = null!;

    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Wallow_AppHost>();
        App = await Builder.BuildAsync();
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();

        if (Builder is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }
}
