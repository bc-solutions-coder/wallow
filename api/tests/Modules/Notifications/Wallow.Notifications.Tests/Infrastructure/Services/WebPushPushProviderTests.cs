using System.Net;
using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class WebPushPushProviderTests
{
    private const string SubscriptionEndpoint = "https://push.example.com/sub/abc123";

    private readonly PushMessage _message = PushMessage.Create(
        TenantId.New(),
        UserId.New(),
        "Test Title",
        "Test Body",
        TimeProvider.System);

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private static ILogger<WebPushPushProvider> CreateLogger()
    {
        return LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<WebPushPushProvider>();
    }
#pragma warning restore CA2000

#pragma warning disable CA2000 // Provider takes ownership of HttpClient
    private static WebPushPushProvider CreateProvider(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler);
        return new WebPushPushProvider(httpClient, CreateLogger());
    }
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, SubscriptionEndpoint);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_UsesDeviceTokenAsEndpointUrl()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, SubscriptionEndpoint);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be(SubscriptionEndpoint);
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SetsTtlHeader()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, SubscriptionEndpoint);

        handler.LastRequest!.Headers.GetValues("TTL").Should().ContainSingle("86400");
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SendsJsonPayloadWithTitleAndBody()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, SubscriptionEndpoint);

        handler.LastRequestBody.Should().Contain("\"title\":\"Test Title\"");
        handler.LastRequestBody.Should().Contain("\"body\":\"Test Body\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Bad subscription")]
    [InlineData(HttpStatusCode.Gone, "Subscription expired")]
    [InlineData(HttpStatusCode.TooManyRequests, "Rate limited")]
    public async Task SendAsync_WhenNonSuccessStatusCode_ReturnsFailureResult(HttpStatusCode statusCode, string responseBody)
    {
        using MockHttpMessageHandler handler = new(statusCode, responseBody);
        WebPushPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, SubscriptionEndpoint);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"WebPush returned {(int)statusCode}");
        result.ErrorMessage.Should().Contain(responseBody);
    }

    [Fact]
    public async Task SendAsync_WhenHttpClientThrows_ReturnsFailureWithExceptionMessage()
    {
        using ThrowingHttpMessageHandler handler = new(new HttpRequestException("DNS resolution failed"));
        WebPushPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, SubscriptionEndpoint);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("DNS resolution failed");
    }

    [Fact]
    public async Task SendAsync_WhenTaskCanceled_ReturnsFailureResult()
    {
        using ThrowingHttpMessageHandler handler = new(new TaskCanceledException("Request timed out"));
        WebPushPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, SubscriptionEndpoint);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task SendAsync_SetsJsonContentType()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, SubscriptionEndpoint);

        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_PayloadContainsTitleAndBodyAtTopLevel()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, SubscriptionEndpoint);

        handler.LastRequestBody.Should().Contain("\"title\":\"Test Title\"");
        handler.LastRequestBody.Should().Contain("\"body\":\"Test Body\"");
    }

    [Fact]
    public async Task SendAsync_WhenNonSuccessStatusCode_ErrorMessageContainsStatusCodeAndResponseBody()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Unauthorized, "Invalid VAPID");
        WebPushPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, SubscriptionEndpoint);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("401");
        result.ErrorMessage.Should().Contain("Invalid VAPID");
    }

    [Fact]
    public void SendAsync_ImplementsIPushProvider()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.Created);
        WebPushPushProvider provider = CreateProvider(handler);

        provider.Should().BeAssignableTo<IPushProvider>();
    }
}
