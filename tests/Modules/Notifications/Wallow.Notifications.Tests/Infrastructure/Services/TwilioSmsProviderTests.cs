using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Notifications.Application.Channels.Sms.Interfaces;
using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public sealed class TwilioSmsProviderTests : IDisposable
{
    private const string AccountSid = "AC_test_account_sid";
    private const string AuthToken = "test_auth_token";
    private const string FromNumber = "+15551234567";

    private readonly IOptions<TwilioSettings> _settings = Options.Create(new TwilioSettings
    {
        AccountSid = AccountSid,
        AuthToken = AuthToken,
        FromNumber = FromNumber
    });

    private MockHttpMessageHandler? _handler;
    private HttpClient? _httpClient;

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private static ILogger<TwilioSmsProvider> CreateLogger()
    {
        return LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<TwilioSmsProvider>();
    }
#pragma warning restore CA2000

    private TwilioSmsProvider CreateSut(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler);
        return new TwilioSmsProvider(_httpClient, _settings, CreateLogger());
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_ReturnsSuccessWithSid()
    {
        string responseJson = """{"sid": "SM_test_message_sid", "status": "queued"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "Hello from tests");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().Be("SM_test_message_sid");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_SendsCorrectAuthorizationHeader()
    {
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, """{"sid": "SM1"}""");
        TwilioSmsProvider sut = CreateSut(_handler);

        await sut.SendAsync("+15559876543", "test");

        string expectedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AccountSid}:{AuthToken}"));
        _handler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        _handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        _handler.LastRequest.Headers.Authorization.Parameter.Should().Be(expectedCredentials);
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_PostsToCorrectUrl()
    {
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, """{"sid": "SM1"}""");
        TwilioSmsProvider sut = CreateSut(_handler);

        await sut.SendAsync("+15559876543", "test");

        string expectedUrl = $"https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json";
        _handler.LastRequest!.RequestUri!.ToString().Should().Be(expectedUrl);
        _handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_SuccessResponse_SendsCorrectFormData()
    {
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, """{"sid": "SM1"}""");
        TwilioSmsProvider sut = CreateSut(_handler);

        await sut.SendAsync("+15559876543", "Hello from tests");

        _handler.LastRequestBody.Should().Contain("To=%2B15559876543");
        _handler.LastRequestBody.Should().Contain("From=%2B15551234567");
        _handler.LastRequestBody.Should().Contain("Body=Hello+from+tests");
    }

    [Fact]
    public async Task SendAsync_SuccessResponseWithoutSid_ReturnsSuccessWithNullSid()
    {
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, """{"status": "queued"}""");
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ErrorResponse_ReturnsFailureWithErrorMessage()
    {
        string responseJson = """{"message": "The 'To' number is not a valid phone number"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("invalid-number", "test");

        result.Success.Should().BeFalse();
        result.MessageSid.Should().BeNull();
        result.ErrorMessage.Should().Be("The 'To' number is not a valid phone number");
    }

    [Fact]
    public async Task SendAsync_ErrorResponseWithoutMessage_FallsBackToReasonPhrase()
    {
        _handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, """{}""");
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SendAsync_HttpException_ReturnsFailureWithExceptionMessage()
    {
        using ThrowingHttpMessageHandler handler = new(new HttpRequestException("Network error"));
        TwilioSmsProvider sut = CreateSut(handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task SendAsync_UnauthorizedResponse_ReturnsFailure()
    {
        string responseJson = """{"message": "Authenticate"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.Unauthorized, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Authenticate");
    }

    [Fact]
    public async Task SendAsync_RateLimitedResponse_ReturnsFailureWithMessage()
    {
        string responseJson = """{"message": "Rate limit exceeded"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Rate limit exceeded");
    }

    [Fact]
    public async Task SendAsync_SuccessWithNullSidValue_ReturnsNullSid()
    {
        string responseJson = """{"sid": null, "status": "queued"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeTrue();
        result.MessageSid.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ErrorResponseWithNullMessageValue_FallsBackToReasonPhrase()
    {
        string responseJson = """{"message": null}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        // Falls back to ReasonPhrase since message is null
    }

    [Fact]
    public async Task SendAsync_ForbiddenResponse_ReturnsFailure()
    {
        string responseJson = """{"message": "Permission denied"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.Forbidden, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Permission denied");
    }

    [Fact]
    public async Task SendAsync_TaskCanceledException_ReturnsFailure()
    {
        using ThrowingHttpMessageHandler handler = new(new TaskCanceledException("Request timed out"));
        TwilioSmsProvider sut = CreateSut(handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task SendAsync_EmptyBody_SendsRequest()
    {
        _handler = new MockHttpMessageHandler(HttpStatusCode.OK, """{"sid": "SM1"}""");
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "");

        result.Success.Should().BeTrue();
        _handler.LastRequestBody.Should().Contain("Body=");
    }

    [Fact]
    public async Task SendAsync_ServiceUnavailable_ReturnsFailure()
    {
        string responseJson = """{"message": "Service temporarily unavailable"}""";
        _handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable, responseJson);
        TwilioSmsProvider sut = CreateSut(_handler);

        SmsDeliveryResult result = await sut.SendAsync("+15559876543", "test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Service temporarily unavailable");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _handler?.Dispose();
        GC.SuppressFinalize(this);
    }
}
