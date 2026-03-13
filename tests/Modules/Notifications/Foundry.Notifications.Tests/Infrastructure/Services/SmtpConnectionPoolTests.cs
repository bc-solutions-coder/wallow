using Foundry.Notifications.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public class SmtpConnectionPoolTests
{
    private readonly IOptions<SmtpSettings> _settings = Options.Create(new SmtpSettings
    {
        Host = "localhost",
        Port = 19999, // No server listening
        UseSsl = false,
        TimeoutSeconds = 1
    });

    private SmtpConnectionPool CreateSut()
    {
        return new SmtpConnectionPool(_settings, NullLogger<SmtpConnectionPool>.Instance);
    }

    [Fact]
    public async Task Dispose_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        SmtpConnectionPool sut = CreateSut();

        await Task.Run(() =>
        {
            sut.Dispose();
            sut.Dispose();
        });

        await sut.DisposeAsync(); // Final cleanup (already disposed, should be no-op)
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes_DoesNotThrow()
    {
        SmtpConnectionPool sut = CreateSut();

        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_AfterDisposeAsync_ThrowsObjectDisposedException()
    {
        SmtpConnectionPool sut = CreateSut();
        await sut.DisposeAsync();

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SendAsync_AfterSyncDispose_ThrowsObjectDisposedException()
    {
        await using SmtpConnectionPool sut = CreateSut();
        await Task.Run(sut.Dispose);

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_AfterSyncDispose_DoesNotThrow()
    {
        await using SmtpConnectionPool sut = CreateSut();
        await Task.Run(sut.Dispose);

        Func<Task> act = async () => await sut.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        SmtpConnectionPool sut = CreateSut();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_NoServerRunning_ThrowsOnConnect()
    {
        SmtpConnectionPool sut = CreateSut();

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithDefaultSettings_CreatesInstance()
    {
        SmtpConnectionPool sut = CreateSut();

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithSslSettings_CreatesInstance()
    {
        IOptions<SmtpSettings> sslSettings = Options.Create(new SmtpSettings
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = true,
            Username = "user",
            Password = "pass",
            TimeoutSeconds = 30
        });

        SmtpConnectionPool sut = new(sslSettings, NullLogger<SmtpConnectionPool>.Instance);

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }
}
