using System.Net;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class FcmPushProviderTests
{
    private const string DeviceToken = "fcm-device-token-123";
    private const string Credential = "test-bearer-credential";

    private readonly PushMessage _message = PushMessage.Create(
        TenantId.New(),
        UserId.New(),
        "Test Title",
        "Test Body",
        TimeProvider.System);

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private static ILogger<FcmPushProvider> CreateLogger()
    {
        return LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<FcmPushProvider>();
    }
#pragma warning restore CA2000

#pragma warning disable CA2000 // Provider takes ownership of HttpClient
    private static FcmPushProvider CreateProvider(HttpMessageHandler handler, string credential = Credential)
    {
        HttpClient httpClient = new(handler);
        return new FcmPushProvider(httpClient, credential, CreateLogger());
    }
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SendsToFcmEndpoint()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://fcm.googleapis.com/v1/projects/-/messages:send");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SetsBearerAuthorizationHeader()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be(Credential);
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SendsJsonPayloadWithTokenAndNotification()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequestBody.Should().Contain("\"token\":\"fcm-device-token-123\"");
        handler.LastRequestBody.Should().Contain("\"title\":\"Test Title\"");
        handler.LastRequestBody.Should().Contain("\"body\":\"Test Body\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Invalid token")]
    [InlineData(HttpStatusCode.Unauthorized, "Auth failed")]
    [InlineData(HttpStatusCode.InternalServerError, "Server error")]
    public async Task SendAsync_WhenNonSuccessStatusCode_ReturnsFailureResult(HttpStatusCode statusCode, string responseBody)
    {
        using MockHttpMessageHandler handler = new(statusCode, responseBody);
        FcmPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"FCM returned {(int)statusCode}");
        result.ErrorMessage.Should().Contain(responseBody);
    }

    [Fact]
    public async Task SendAsync_WhenHttpClientThrows_ReturnsFailureWithExceptionMessage()
    {
        using ThrowingHttpMessageHandler handler = new(new HttpRequestException("Network error"));
        FcmPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Network error");
    }

    [Fact]
    public async Task SendAsync_WhenTaskCanceled_ReturnsFailureResult()
    {
        using ThrowingHttpMessageHandler handler = new(new TaskCanceledException("Timeout"));
        FcmPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Timeout");
    }

    [Fact]
    public async Task SendAsync_SetsJsonContentType()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_PayloadContainsMessageWrapper()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequestBody.Should().Contain("\"message\":");
        handler.LastRequestBody.Should().Contain("\"notification\":");
    }

    [Fact]
    public async Task SendAsync_WhenNonSuccessStatusCode_ErrorMessageContainsStatusCodeAndResponseBody()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.NotFound, "{\"error\":\"NOT_FOUND\"}");
        FcmPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("404");
        result.ErrorMessage.Should().Contain("NOT_FOUND");
    }

    [Fact]
    public void SendAsync_ImplementsIPushProvider()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        FcmPushProvider provider = CreateProvider(handler);

        provider.Should().BeAssignableTo<IPushProvider>();
    }
}
