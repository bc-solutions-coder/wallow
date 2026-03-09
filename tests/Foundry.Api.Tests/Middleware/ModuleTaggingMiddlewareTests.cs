using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Foundry.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Foundry.Api.Tests.Middleware;

public sealed class ModuleTaggingMiddlewareTests : IDisposable
{
    private readonly ActivitySource _activitySource = new("Foundry.Tests.ModuleTagging");

    [Fact]
    public async Task InvokeAsync_WithMatchingModuleNamespace_SetsModuleTag()
    {
        using ActivityListener listener = CreateListener();
        using Activity? activity = _activitySource.StartActivity();
        activity.Should().NotBeNull();

        bool nextCalled = false;
        ModuleTaggingMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = CreateHttpContextWithEndpoint("Foundry.Billing.Api.Controllers");

        await sut.InvokeAsync(httpContext);

        activity.GetTagItem("foundry.module").Should().Be("Billing");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithNonMatchingNamespace_DoesNotSetTag()
    {
        using ActivityListener listener = CreateListener();
        using Activity? activity = _activitySource.StartActivity();
        activity.Should().NotBeNull();

        ModuleTaggingMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = CreateHttpContextWithEndpoint("SomeOther.Namespace");

        await sut.InvokeAsync(httpContext);

        activity.GetTagItem("foundry.module").Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithNoEndpoint_DoesNotSetTag()
    {
        using ActivityListener listener = CreateListener();
        using Activity? activity = _activitySource.StartActivity();
        activity.Should().NotBeNull();

        ModuleTaggingMiddleware sut = new(_ => Task.CompletedTask);
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext);

        activity.GetTagItem("foundry.module").Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithNoActivity_CallsNextWithoutError()
    {
        bool nextCalled = false;
        ModuleTaggingMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = CreateHttpContextWithEndpoint("Foundry.Identity.Api.Controllers");

        await sut.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        bool nextCalled = false;
        ModuleTaggingMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    private static DefaultHttpContext CreateHttpContextWithEndpoint(string controllerNamespace)
    {
        AssemblyName assemblyName = new("DynamicTestAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            $"{controllerNamespace}.TestController",
            TypeAttributes.Public);
        Type dynamicType = typeBuilder.CreateType();

        ControllerActionDescriptor descriptor = new()
        {
            ControllerTypeInfo = dynamicType.GetTypeInfo()
        };

        Endpoint endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/"),
            0,
            new EndpointMetadataCollection(descriptor),
            "test");

        DefaultHttpContext httpContext = new();
        httpContext.SetEndpoint(endpoint);
        return httpContext;
    }

    private ActivityListener CreateListener()
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == _activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public void Dispose()
    {
        _activitySource.Dispose();
    }
}
