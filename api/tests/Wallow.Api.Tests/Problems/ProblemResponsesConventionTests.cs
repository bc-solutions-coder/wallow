using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Wallow.Shared.Api.Problems;

namespace Wallow.Api.Tests.Problems;

public class ProblemResponsesConventionTests
{
    private readonly ProblemResponsesConvention _sut = new();

    [Fact]
    public void Apply_DeclaresTheSharedProblemStatuses()
    {
        ActionModel action = Build(nameof(SampleController.NoInput));

        _sut.Apply(action);

        Declared(action).Keys.Should().BeEquivalentTo([400, 401, 403, 404, 429, 500]);
        Declared(action).Values.Should().AllBeEquivalentTo(typeof(ProblemDetails));
    }

    [Fact]
    public void Apply_ActionWithBoundInput_Declares400AsValidationProblem()
    {
        ActionModel action = Build(nameof(SampleController.WithBody));

        _sut.Apply(action);

        Declared(action)[400].Should().Be<HttpValidationProblemDetails>();
    }

    [Fact]
    public void Apply_ActionWithOnlyServicesAndCancellation_Declares400AsPlainProblem()
    {
        ActionModel action = Build(nameof(SampleController.FromServices));

        _sut.Apply(action);

        Declared(action)[400].Should().Be<ProblemDetails>();
    }

    [Fact]
    public void Apply_LeavesAStatusTheActionAlreadyDeclares()
    {
        ActionModel action = Build(nameof(SampleController.Declared404));

        _sut.Apply(action);

        action.Filters.OfType<IApiResponseMetadataProvider>().Where(p => p.StatusCode == 404)
            .Should().ContainSingle().Which.Type.Should().Be<SampleResponse>();
    }

    [Fact]
    public void Apply_LeavesAStatusTheControllerAlreadyDeclares()
    {
        ActionModel action = Build(nameof(SampleController.NoInput));
        action.Controller.Filters.Add(new ProducesResponseTypeAttribute(typeof(SampleResponse), 401));

        _sut.Apply(action);

        action.Filters.OfType<IApiResponseMetadataProvider>().Should().NotContain(p => p.StatusCode == 401);
    }

    [Theory]
    [InlineData(nameof(SampleController.TypedResult))]
    [InlineData(nameof(SampleController.TypedResultAsync))]
    [InlineData(nameof(SampleController.PlainValue))]
    public void Apply_ActionWithNoDeclaredResponses_KeepsTheInferred200(string methodName)
    {
        ActionModel action = Build(methodName);

        _sut.Apply(action);

        Declared(action).Should().ContainKey(200).WhoseValue.Should().Be<SampleResponse>();
    }

    [Fact]
    public void Apply_ABareProducesAttribute_DoesNotHideTheInferred200()
    {
        ActionModel action = Build(nameof(SampleController.TypedResult));
        action.Controller.Filters.Add(new ProducesAttribute("application/json"));

        _sut.Apply(action);

        Declared(action).Should().ContainKey(200).WhoseValue.Should().Be<SampleResponse>();
    }

    [Theory]
    [InlineData(nameof(SampleController.NoInput))]
    [InlineData(nameof(SampleController.Declared404))]
    [InlineData(nameof(SampleController.TypedResultWithDeclared404))]
    public void Apply_DoesNotInventA200TheExplorerWouldNotInfer(string methodName)
    {
        ActionModel action = Build(methodName);

        _sut.Apply(action);

        Declared(action).Should().NotContainKey(200);
    }

    private static Dictionary<int, Type> Declared(ActionModel action) =>
        action.Filters.OfType<IApiResponseMetadataProvider>()
            .Where(p => p.Type is not null)
            .ToDictionary(p => p.StatusCode, p => p.Type!);

    private static ActionModel Build(string methodName)
    {
        TypeInfo type = typeof(SampleController).GetTypeInfo();
        ControllerModel controller = new(type, type.GetCustomAttributes(inherit: true));
        MethodInfo method = type.GetMethod(methodName)!;
        object[] attributes = method.GetCustomAttributes(inherit: true);
        ActionModel action = new(method, attributes) { Controller = controller };
        foreach (IFilterMetadata filter in attributes.OfType<IFilterMetadata>())
        {
            action.Filters.Add(filter);
        }

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            object[] parameterAttributes = parameter.GetCustomAttributes(inherit: true);
            action.Parameters.Add(new ParameterModel(parameter, parameterAttributes)
            {
                Action = action,
                BindingInfo = BindingInfo.GetBindingInfo(parameterAttributes),
            });
        }

        return action;
    }

    private sealed record SampleResponse;

    private sealed class SampleController : ControllerBase
    {
        public OkObjectResult NoInput(CancellationToken cancellationToken) => Ok(cancellationToken.IsCancellationRequested);

        public OkObjectResult WithBody([FromBody] SampleResponse body) => Ok(body);

        public OkObjectResult FromServices([FromServices] object service, CancellationToken cancellationToken) =>
            Ok((service, cancellationToken.IsCancellationRequested));

        [ProducesResponseType(typeof(SampleResponse), 404)]
        public OkResult Declared404() => Ok();

        public ActionResult<SampleResponse> TypedResult() => Ok(new SampleResponse());

        public Task<ActionResult<SampleResponse>> TypedResultAsync() =>
            Task.FromResult<ActionResult<SampleResponse>>(Ok(new SampleResponse()));

        public SampleResponse PlainValue() => new();

        [ProducesResponseType(typeof(SampleResponse), 404)]
        public ActionResult<SampleResponse> TypedResultWithDeclared404() => Ok(new SampleResponse());
    }
}
