using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace Wallow.AppHost.Tests;

/// <summary>
/// Builds the Aspire application model for inspection without starting its resources.
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
