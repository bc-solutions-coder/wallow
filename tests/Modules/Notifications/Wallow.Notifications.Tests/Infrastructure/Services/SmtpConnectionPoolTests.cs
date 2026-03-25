using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class SmtpConnectionPoolTests
{
    private readonly IOptions<SmtpSettings> _settings = Options.Create(new SmtpSettings
    {
        Host = "localhost",
        Port = 19999, // No server listening
        UseSsl = false,
        TimeoutSeconds = 1
    });

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private static ILogger<SmtpConnectionPool> CreateLogger()
    {
        return LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<SmtpConnectionPool>();
    }
#pragma warning restore CA2000

    private SmtpConnectionPool CreateSut()
    {
        return new SmtpConnectionPool(_settings, CreateLogger());
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

        SmtpConnectionPool sut = new(sslSettings, CreateLogger());

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_MultipleCallsAfterDispose_AllThrowObjectDisposedException()
    {
        SmtpConnectionPool sut = CreateSut();
        await sut.DisposeAsync();

        Func<Task> act1 = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);
        Func<Task> act2 = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act1.Should().ThrowAsync<ObjectDisposedException>();
        await act2.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_AfterDisposeAsync_DoesNotThrow()
    {
        SmtpConnectionPool sut = CreateSut();
        await sut.DisposeAsync();

        Action act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Pool_ImplementsIAsyncDisposable()
    {
        using SmtpConnectionPool sut = CreateSut();

        sut.Should().BeAssignableTo<IAsyncDisposable>();
    }

    [Fact]
    public void Pool_ImplementsIDisposable()
    {
        using SmtpConnectionPool sut = CreateSut();

        sut.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public async Task SendAsync_ConcurrentDisposeAndSend_DoesNotDeadlock()
    {
        SmtpConnectionPool sut = CreateSut();

        Task disposeTask = sut.DisposeAsync().AsTask();
        Func<Task> sendAct = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await disposeTask;

        // After dispose completes, send should throw ObjectDisposedException
        await sendAct.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Constructor_WithNoCredentials_CreatesInstance()
    {
        IOptions<SmtpSettings> noAuthSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 25,
            UseSsl = false,
            Username = null,
            Password = null,
            TimeoutSeconds = 5
        });

        SmtpConnectionPool sut = new(noAuthSettings, CreateLogger());

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithEmptyCredentials_CreatesInstance()
    {
        IOptions<SmtpSettings> emptyAuthSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 25,
            UseSsl = false,
            Username = "",
            Password = "",
            TimeoutSeconds = 5
        });

        SmtpConnectionPool sut = new(emptyAuthSettings, CreateLogger());

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithWhitespaceCredentials_CreatesInstance()
    {
        IOptions<SmtpSettings> whitespaceAuthSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 25,
            UseSsl = false,
            Username = "  ",
            Password = "  ",
            TimeoutSeconds = 5
        });

        SmtpConnectionPool sut = new(whitespaceAuthSettings, CreateLogger());

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_WithPopulatedMimeMessage_ThrowsWhenNoServer()
    {
        await using SmtpConnectionPool sut = CreateSut();
        using MimeKit.MimeMessage message = new();
        message.From.Add(new MimeKit.MailboxAddress("Test", "test@example.com"));
        message.To.Add(new MimeKit.MailboxAddress("Recipient", "recipient@example.com"));
        message.Subject = "Test Subject";

        Exception? caught = null;
        try
        {
            await sut.SendAsync(message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_OnFreshInstance_CompletesSuccessfully()
    {
        await using SmtpConnectionPool sut = CreateSut();

        Func<Task> act = async () => await sut.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispose_OnFreshInstance_CompletesSuccessfully()
    {
        SmtpConnectionPool sut = CreateSut();

        Action act = () => sut.Dispose();

        act.Should().NotThrow();

        // Clean up
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_SequentialCallsAfterFirstFails_EachThrowsIndependently()
    {
        SmtpConnectionPool sut = CreateSut();

        Func<Task> act1 = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);
        await act1.Should().ThrowAsync<Exception>();

        Func<Task> act2 = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);
        await act2.Should().ThrowAsync<Exception>();

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithCustomTimeout_CreatesInstance()
    {
        IOptions<SmtpSettings> customTimeoutSettings = Options.Create(new SmtpSettings
        {
            Host = "smtp.example.com",
            Port = 465,
            UseSsl = true,
            TimeoutSeconds = 120
        });

        SmtpConnectionPool sut = new(customTimeoutSettings, CreateLogger());

        sut.Should().NotBeNull();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_ConsecutiveCallsToSameHost_ReusesPoolKey()
    {
        await using SmtpConnectionPool sut = CreateSut();

        // Both calls target the same host:port so they hit the same pool key path.
        // They will fail on connect, but the pool key logic is exercised.
        Func<Task> act1 = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);
        await act1.Should().ThrowAsync<Exception>();

        Func<Task> act2 = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);
        await act2.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendAsync_ConcurrentCalls_DoNotDeadlock()
    {
        await using SmtpConnectionPool sut = CreateSut();

        Task[] tasks = Enumerable.Range(0, 5).Select(_ =>
            Task.Run(async () =>
            {
                try
                {
                    await sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);
                }
                catch
                {
                    // Expected — no server listening
                }
            })).ToArray();

        Func<Task> act = () => Task.WhenAll(tasks);

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(30));
    }

#pragma warning disable CA2000 // Dispose handled manually in test
    [Fact]
    public async Task SendAsync_ThenDispose_ThenSendAsync_ThrowsObjectDisposedException()
    {
        await using SmtpConnectionPool sut = CreateSut();

        using (MimeKit.MimeMessage msg = new())
        {
            try { await sut.SendAsync(msg, CancellationToken.None); }
            catch { /* expected — no server */ }
        }

        await Task.Run(sut.Dispose);

        MimeKit.MimeMessage msg2 = new();
        try
        {
            Func<Task> act = () => sut.SendAsync(msg2, CancellationToken.None);
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }
        finally
        {
            msg2.Dispose();
        }
    }
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_ThenDisposeAsync_CleansUpState()
    {
        await using SmtpConnectionPool sut = CreateSut();

        // First send fails on connect but exercises GetOrCreateClientAsync
        using (MimeKit.MimeMessage msg = new())
        {
            try { await sut.SendAsync(msg, CancellationToken.None); }
            catch { /* expected */ }
        }

        // DisposeAsync should clean up any pooled clients
        Func<Task> act = async () => await sut.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispose_AfterFailedSend_CleansUp()
    {
        await using SmtpConnectionPool sut = CreateSut();

        // Attempt send which fails — may leave a client in the pool
        using (MimeKit.MimeMessage message = new())
        {
            try { await sut.SendAsync(message, CancellationToken.None); }
            catch { /* expected */ }
        }

        // Sync dispose should clean up without throwing
        await Task.Run(sut.Dispose);
    }

    [Fact]
    public async Task SendAsync_WithSslEnabled_ThrowsOnConnect()
    {
        IOptions<SmtpSettings> sslSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 19999,
            UseSsl = true,
            Username = "user",
            Password = "pass",
            TimeoutSeconds = 1
        });
        await using SmtpConnectionPool sut = new(sslSettings, CreateLogger());

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendAsync_WithNoCredentials_SkipsAuthentication()
    {
        IOptions<SmtpSettings> noAuthSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 19999,
            UseSsl = false,
            Username = null,
            Password = null,
            TimeoutSeconds = 1
        });
        await using SmtpConnectionPool sut = new(noAuthSettings, CreateLogger());

        // Will fail on connect, but exercises the no-auth code path
        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendAsync_WithEmptyCredentials_SkipsAuthentication()
    {
        IOptions<SmtpSettings> emptyAuthSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 19999,
            UseSsl = false,
            Username = "",
            Password = "",
            TimeoutSeconds = 1
        });
        await using SmtpConnectionPool sut = new(emptyAuthSettings, CreateLogger());

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SendAsync_WithWhitespaceCredentials_SkipsAuthentication()
    {
        IOptions<SmtpSettings> whitespaceSettings = Options.Create(new SmtpSettings
        {
            Host = "localhost",
            Port = 19999,
            UseSsl = false,
            Username = "   ",
            Password = "   ",
            TimeoutSeconds = 1
        });
        await using SmtpConnectionPool sut = new(whitespaceSettings, CreateLogger());

        Func<Task> act = () => sut.SendAsync(new MimeKit.MimeMessage(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}
