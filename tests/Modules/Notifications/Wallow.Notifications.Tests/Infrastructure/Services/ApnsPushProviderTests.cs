using System.Net;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class ApnsPushProviderTests
{
    private const string DeviceToken = "abc123-device-token";

    private readonly PushMessage _message = PushMessage.Create(
        TenantId.New(),
        UserId.New(),
        "Test Title",
        "Test Body",
        TimeProvider.System);

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private static ILogger<ApnsPushProvider> CreateLogger()
    {
        return LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<ApnsPushProvider>();
    }
#pragma warning restore CA2000

#pragma warning disable CA2000 // Provider takes ownership of HttpClient
    private static ApnsPushProvider CreateProvider(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler);
        return new ApnsPushProvider(httpClient, CreateLogger());
    }
#pragma warning restore CA2000

    [Fact]
    public async Task SendAsync_WhenSuccessful_ReturnsSuccessResult()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SendsToCorrectApnsEndpoint()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be($"https://api.push.apple.com/3/device/{DeviceToken}");
        handler.LastRequest.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SetsApnsHeaders()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequest!.Headers.GetValues("apns-topic").Should().ContainSingle("com.wallow.app");
        handler.LastRequest.Headers.GetValues("apns-push-type").Should().ContainSingle("alert");
    }

    [Fact]
    public async Task SendAsync_WhenSuccessful_SendsJsonPayloadWithTitleAndBody()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequestBody.Should().Contain("\"title\":\"Test Title\"");
        handler.LastRequestBody.Should().Contain("\"body\":\"Test Body\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Bad Request")]
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(HttpStatusCode.InternalServerError, "Server Error")]
    public async Task SendAsync_WhenNonSuccessStatusCode_ReturnsFailureResult(HttpStatusCode statusCode, string responseBody)
    {
        using MockHttpMessageHandler handler = new(statusCode, responseBody);
        ApnsPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain($"APNs returned {(int)statusCode}");
        result.ErrorMessage.Should().Contain(responseBody);
    }

    [Fact]
    public async Task SendAsync_WhenHttpClientThrows_ReturnsFailureWithExceptionMessage()
    {
        using ThrowingHttpMessageHandler handler = new(new HttpRequestException("Connection refused"));
        ApnsPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Connection refused");
    }

    [Fact]
    public async Task SendAsync_WhenTaskCanceled_ReturnsFailureResult()
    {
        using ThrowingHttpMessageHandler handler = new(new TaskCanceledException("Request timed out"));
        ApnsPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task SendAsync_SetsJsonContentType()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendAsync_PayloadContainsApsStructure()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        await provider.SendAsync(_message, DeviceToken);

        handler.LastRequestBody.Should().Contain("\"aps\":");
        handler.LastRequestBody.Should().Contain("\"alert\":");
    }

    [Fact]
    public async Task SendAsync_WhenNonSuccessStatusCode_ErrorMessageContainsStatusCodeAndResponseBody()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.NotFound, "{\"reason\":\"BadDeviceToken\"}");
        ApnsPushProvider provider = CreateProvider(handler);

        PushDeliveryResult result = await provider.SendAsync(_message, DeviceToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("404");
        result.ErrorMessage.Should().Contain("BadDeviceToken");
    }

    [Fact]
    public void SendAsync_ImplementsIPushProvider()
    {
        using MockHttpMessageHandler handler = new(HttpStatusCode.OK);
        ApnsPushProvider provider = CreateProvider(handler);

        provider.Should().BeAssignableTo<IPushProvider>();
    }
}
