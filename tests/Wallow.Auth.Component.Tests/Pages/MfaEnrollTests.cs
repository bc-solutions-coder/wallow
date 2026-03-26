using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class MfaEnrollTests : BunitContext
{
    public MfaEnrollTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
    }

    [Fact]
    public void Renders_InitialSetupPageWithBeginButton()
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        Services.AddSingleton(httpClientFactory);

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        cut.Markup.Should().Contain("Set up two-factor authentication");
        cut.Markup.Should().Contain("Begin setup");
        cut.Markup.Should().Contain("authenticator app");
    }

    [Fact]
    public async Task BeginSetup_CallsApiAndShowsSecret()
    {
        using HttpResponseMessage enrollResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"secret":"TESTSECRET","qrUri":"otpauth://totp/test"}""",
                Encoding.UTF8, "application/json")
        };
        using FakeHttpMessageHandler handler = new(enrollResponse);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5001") };

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("AuthApi").Returns(httpClient);
        Services.AddSingleton(httpClientFactory);

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Begin setup"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("TESTSECRET");
        cut.Markup.Should().Contain("otpauth://totp/test");
        cut.Markup.Should().Contain("Verification code");
    }

    [Fact]
    public async Task BeginSetup_Failure_ShowsError()
    {
        using HttpResponseMessage errorResponse = new(HttpStatusCode.InternalServerError);
        using FakeHttpMessageHandler handler = new(errorResponse);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5001") };

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("AuthApi").Returns(httpClient);
        Services.AddSingleton(httpClientFactory);

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Begin setup"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Failed to start MFA enrollment");
    }

    [Fact]
    public async Task ConfirmCode_Success_ShowsBackupCodes()
    {
        using HttpResponseMessage enrollResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"secret":"TESTSECRET","qrUri":"otpauth://totp/test"}""",
                Encoding.UTF8, "application/json")
        };
        using HttpResponseMessage confirmResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"succeeded":true,"backupCodes":["CODE1","CODE2","CODE3"]}""",
                Encoding.UTF8, "application/json")
        };
        using FakeHttpMessageHandler handler = new([enrollResponse, confirmResponse]);
        using HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:5001") };

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("AuthApi").Returns(httpClient);
        Services.AddSingleton(httpClientFactory);

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement beginButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Begin setup"));
        await beginButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("MFA enabled successfully");
        cut.Markup.Should().Contain("CODE1");
        cut.Markup.Should().Contain("CODE2");
        cut.Markup.Should().Contain("CODE3");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _responses = new Queue<HttpResponseMessage>();
            _responses.Enqueue(response);
        }

        public FakeHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
