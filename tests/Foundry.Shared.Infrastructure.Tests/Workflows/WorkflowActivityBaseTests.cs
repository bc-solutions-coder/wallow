using System.Reflection;
using Elsa.Common;
using Elsa.Mediator.Contracts;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.CommitStates;
using Elsa.Workflows.Models;
using Foundry.Shared.Infrastructure.Workflows.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Foundry.Shared.Infrastructure.Tests.Workflows;

public class TestActivity : WorkflowActivityBase
{
    public override string ModuleName => "TestModule";

    public bool WasExecuted { get; private set; }

    protected override ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        WasExecuted = true;
        return ValueTask.CompletedTask;
    }
}

public class DefaultModuleActivity : WorkflowActivityBase
{
    protected override ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        return ValueTask.CompletedTask;
    }
}

public class ThrowingActivity : WorkflowActivityBase
{
    public override string ModuleName => "ErrorModule";

    protected override ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        throw new InvalidOperationException("Activity failed");
    }
}

public class WorkflowActivityBaseTests
{
    [Fact]
    public void ModuleName_WhenOverridden_ReturnsCustomModuleName()
    {
        TestActivity activity = new();

        string moduleName = activity.ModuleName;

        moduleName.Should().Be("TestModule");
    }

    [Fact]
    public void ModuleName_WhenNotOverridden_ReturnsShared()
    {
        DefaultModuleActivity activity = new();

        string moduleName = activity.ModuleName;

        moduleName.Should().Be("Shared");
    }

    [Fact]
    public void WorkflowActivityBase_DerivedClass_CanBeInstantiated()
    {
        TestActivity activity = new();

        activity.Should().NotBeNull();
        activity.Should().BeAssignableTo<WorkflowActivityBase>();
        activity.Should().BeAssignableTo<CodeActivity>();
    }

    [Fact]
    public void WorkflowActivityBase_IsAbstract()
    {
        Type baseType = typeof(WorkflowActivityBase);

        baseType.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void WorkflowActivityBase_InheritsFromCodeActivity()
    {
        Type baseType = typeof(WorkflowActivityBase);

        baseType.Should().BeDerivedFrom<CodeActivity>();
    }

    [Fact]
    public void Type_WhenInstantiated_IsSetFromClassName()
    {
        TestActivity activity = new();

        activity.Type.Should().NotBeNullOrEmpty();
        activity.Type.Should().Contain(nameof(TestActivity));
    }

    [Fact]
    public void Type_DifferentDerivedClasses_HaveDistinctTypes()
    {
        TestActivity testActivity = new();
        DefaultModuleActivity defaultActivity = new();

        testActivity.Type.Should().NotBe(defaultActivity.Type);
    }

    [Fact]
    public void Version_WhenInstantiated_DefaultsToOne()
    {
        TestActivity activity = new();

        activity.Version.Should().Be(1);
    }

    [Fact]
    public void ExecuteActivityAsync_IsAbstract()
    {
        MethodInfo? method = typeof(WorkflowActivityBase).GetMethod(
            "ExecuteActivityAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();
        method.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void ExecuteActivityAsync_IsProtected()
    {
        MethodInfo? method = typeof(WorkflowActivityBase).GetMethod(
            "ExecuteActivityAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();
        method.IsFamily.Should().BeTrue("ExecuteActivityAsync should be protected");
    }

    [Fact]
    public void ExecuteAsync_OverridesBaseClassMethod()
    {
        MethodInfo? method = typeof(WorkflowActivityBase).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();
        method.DeclaringType.Should().Be<WorkflowActivityBase>();
        method.GetBaseDefinition().DeclaringType.Should().NotBe<WorkflowActivityBase>(
            "ExecuteAsync should override a base class method");
    }

    [Fact]
    public void ExecuteActivityAsync_AcceptsActivityExecutionContext()
    {
        MethodInfo? method = typeof(WorkflowActivityBase).GetMethod(
            "ExecuteActivityAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();
        ParameterInfo[] parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be<ActivityExecutionContext>();
    }

    [Fact]
    public void ExecuteActivityAsync_ReturnsValueTask()
    {
        MethodInfo? method = typeof(WorkflowActivityBase).GetMethod(
            "ExecuteActivityAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<ValueTask>();
    }

    [Fact]
    public void ModuleName_IsVirtual_AllowsOverride()
    {
        PropertyInfo? property = typeof(WorkflowActivityBase).GetProperty(nameof(WorkflowActivityBase.ModuleName));

        property.Should().NotBeNull();
        property.GetMethod!.IsVirtual.Should().BeTrue();
        property.GetMethod.IsFinal.Should().BeFalse();
    }

    [Fact]
    public void ThrowingActivity_ModuleName_ReturnsErrorModule()
    {
        ThrowingActivity activity = new();

        activity.ModuleName.Should().Be("ErrorModule");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulActivity_CallsExecuteActivityAsync()
    {
        TestActivity activity = new();
        ActivityExecutionContext context = CreateActivityExecutionContext(activity);

        await InvokeExecuteAsync(activity, context);

        activity.WasExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulActivity_CompletesWithoutException()
    {
        DefaultModuleActivity activity = new();
#pragma warning disable CA2000 // ServiceProvider created inside factory is test scaffolding
        ActivityExecutionContext context = CreateActivityExecutionContext(activity);
#pragma warning restore CA2000

        Func<Task> act = () => InvokeExecuteAsync(activity, context).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithThrowingActivity_PropagatesException()
    {
        ThrowingActivity activity = new();
#pragma warning disable CA2000 // ServiceProvider created inside factory is test scaffolding
        ActivityExecutionContext context = CreateActivityExecutionContext(activity);
#pragma warning restore CA2000

        Func<Task> act = () => InvokeExecuteAsync(activity, context).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Activity failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomModuleName_LogsCorrectModule()
    {
        using FakeLoggerProvider loggerProvider = new();
        TestActivity activity = new();
        ActivityExecutionContext context = CreateActivityExecutionContext(activity, loggerProvider);

        await InvokeExecuteAsync(activity, context);

        loggerProvider.LogEntries.Should().Contain(e => e.Contains("TestModule"));
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultModuleName_LogsSharedModule()
    {
        using FakeLoggerProvider loggerProvider = new();
        DefaultModuleActivity activity = new();
        ActivityExecutionContext context = CreateActivityExecutionContext(activity, loggerProvider);

        await InvokeExecuteAsync(activity, context);

        loggerProvider.LogEntries.Should().Contain(e => e.Contains("Shared"));
    }

    [Fact]
    public async Task ExecuteAsync_LogsActivityTypeName()
    {
        using FakeLoggerProvider loggerProvider = new();
        TestActivity activity = new();
        ActivityExecutionContext context = CreateActivityExecutionContext(activity, loggerProvider);

        await InvokeExecuteAsync(activity, context);

        loggerProvider.LogEntries.Should().Contain(e => e.Contains("TestActivity"));
    }

    [Fact]
    public async Task ExecuteAsync_LogsExecutingAndCompleted()
    {
        using FakeLoggerProvider loggerProvider = new();
        TestActivity activity = new();
        ActivityExecutionContext context = CreateActivityExecutionContext(activity, loggerProvider);

        await InvokeExecuteAsync(activity, context);

        loggerProvider.LogEntries.Should().HaveCountGreaterThanOrEqualTo(2,
            "should log both Executing and Completed messages");
    }

    private static ActivityExecutionContext CreateActivityExecutionContext(
        WorkflowActivityBase activity,
        FakeLoggerProvider? loggerProvider = null)
    {
        activity.Id = "test-activity-1";
        activity.NodeId = "test-node-1";

        ISystemClock clock = Substitute.For<ISystemClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        ServiceCollection services = new();
        if (loggerProvider != null)
        {
            services.AddLogging(b => b.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Debug));
        }
        else
        {
            services.AddLogging();
        }

        services.AddSingleton(clock);
        services.AddSingleton(Substitute.For<IActivityRegistry>());
        services.AddSingleton(Substitute.For<IActivityRegistryLookupService>());
        services.AddSingleton(Substitute.For<IHasher>());
        services.AddSingleton(Substitute.For<ICommitStateHandler>());
        services.AddSingleton(Substitute.For<IActivitySchedulerFactory>());
        services.AddSingleton(Substitute.For<IIdentityGenerator>());
        services.AddSingleton(Substitute.For<INotificationSender>());

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Workflow workflow = new();
        workflow.Id = "test-workflow";
        workflow.Root = activity;

        ActivityNode rootNode = new(activity, "root");
        WorkflowGraph workflowGraph = new(workflow, rootNode, new[] { rootNode });

        static ValueTask execDelegate(ActivityExecutionContext _) => ValueTask.CompletedTask;

        ConstructorInfo wecCtor = typeof(WorkflowExecutionContext)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];

        WorkflowExecutionContext wec = (WorkflowExecutionContext)wecCtor.Invoke(new object?[]
        {
            serviceProvider, workflowGraph, "wf-instance-1", null, null,
            new Dictionary<string, object>(), new Dictionary<string, object>(),
            (ExecuteActivityDelegate)execDelegate, null, Array.Empty<ActivityIncident>(), Array.Empty<Bookmark>(),
            DateTimeOffset.UtcNow, CancellationToken.None
        });

        return new ActivityExecutionContext(
            "aec-1", wec, null!, activity, new ActivityDescriptor(),
            DateTimeOffset.UtcNow, null, clock, CancellationToken.None);
    }

    private static async ValueTask InvokeExecuteAsync(
        WorkflowActivityBase activity,
        ActivityExecutionContext context)
    {
        MethodInfo method = typeof(WorkflowActivityBase).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        ValueTask result = (ValueTask)method.Invoke(activity, new object[] { context })!;
        await result;
    }
}

internal sealed class FakeLoggerProvider : ILoggerProvider
{
    private readonly List<string> _logEntries = new();

    public IReadOnlyList<string> LogEntries => _logEntries;

    public ILogger CreateLogger(string categoryName) => new FakeLogger(_logEntries);

    public void Dispose() { }

    private sealed class FakeLogger(List<string> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            new NoOpDisposable();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(formatter(state, exception));
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
