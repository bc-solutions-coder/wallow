using System.Net;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public class ApnsPushProviderTests
{
    private const string DeviceToken = "abc123-device-token";

    private readonly PushMessage _message = PushMessage.Create(
        TenantId.New(),
        UserId.New(),
        "Test Title",
        "Test Body",
        TimeProvider.System);

#pragma warning disable CA2000 // Provider takes ownership of HttpClient
    private static ApnsPushProvider CreateProvider(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler);
        return new ApnsPushProvider(httpClient, NullLogger<ApnsPushProvider>.Instance);
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

        handler.LastRequest!.Headers.GetValues("apns-topic").Should().ContainSingle("com.foundry.app");
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
}
