using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Wallow.ServiceDefaults;

#pragma warning disable CA1024 // Keep callable MemberData factories.

namespace Wallow.Api.Tests.OpenApi;

/// <summary>
/// Checks reflected controller metadata for untyped success responses.
/// The SDK openapi-regen test checks the generated document separately.
/// </summary>
public class TypedSuccessResponseTests
{
    /// <summary>
    /// Tag excluded from the public document by the test-support transformer.
    /// </summary>
    private const string TestSupportTagName = "Test Support";

    public static IEnumerable<object[]> GetModuleNames()
    {
        foreach (string moduleName in DiscoverModuleNames())
        {
            yield return [moduleName];
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void ModuleControllerActions_DeclareTypedSuccessResponses(string moduleName)
    {
        Assembly apiAssembly = Assembly.Load($"Wallow.{moduleName}.Api");

        List<string> offenders = FindActionsWithUntypedSuccessResponse(apiAssembly.GetTypes());

        offenders.Should().BeEmpty(
            $"every {moduleName} action that answers a body-bearing 2xx must declare a typed body so " +
            $"the generated SDK client is typed rather than unknown. {offenders.Count} action(s) still " +
            $"emit an untyped success response: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void AliveEndpoint_DeclaresTypedSuccessResponse()
    {
        using WebApplication app = WebApplication.CreateSlimBuilder().Build();
        app.MapDefaultEndpoints();

        RouteEndpoint aliveEndpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => string.Equals(endpoint.RoutePattern.RawText, "/alive", StringComparison.Ordinal));

        bool excludedFromDocument = aliveEndpoint.Metadata
            .GetMetadata<IExcludeFromDescriptionMetadata>()?.ExcludeFromDescription == true;

        bool declaresTypedSuccess = aliveEndpoint.Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Any(metadata => metadata.StatusCode == StatusCodes.Status200OK && IsTypedBody(metadata.Type));

        (excludedFromDocument || declaresTypedSuccess).Should().BeTrue(
            "GET /alive emits an untyped 200 into the v1 OpenAPI document, so it must either declare " +
            "a typed 200 body or be excluded from the API description");
    }

    [Fact]
    public void Detector_CatchesABareCreatedOnAnActionWithNoInferableBody()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(BareCreatedFixtureController)]);

        offenders.Should().ContainSingle().Which.Should().Be("BareCreatedFixtureController.Create");
    }

    [Fact]
    public void Detector_CatchesAnUntypedAcceptedAlongsideATypedOk()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(TypedOkWithBareAcceptedFixtureController)]);

        offenders.Should().ContainSingle().Which.Should().Be("TypedOkWithBareAcceptedFixtureController.Enqueue");
    }

    [Fact]
    public void Detector_CatchesAnActionThatDeclaresNoResponseMetadataAtAll()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(NoResponseMetadataFixtureController)]);

        offenders.Should().ContainSingle().Which.Should().Be("NoResponseMetadataFixtureController.Get");
    }

    /// <summary>
    /// Accepts a declared 201 when the action supplies an ActionResult body type.
    /// </summary>
    [Fact]
    public void Detector_PassesABareCreatedWhoseBodyIsInferredFromActionResultOfT()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(InferredCreatedFixtureController)]);

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Detector_PassesAnExplicitlyTypedCreated()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(TypedCreatedFixtureController)]);

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Detector_PassesAnActionThatAnswersOnly204WhichCarriesNoBodyByDefinition()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(NoContentFixtureController)]);

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Detector_PassesABareNonSuccessResponseWhichNeedsNoSchema()
    {
        List<string> offenders = FindActionsWithUntypedSuccessResponse([typeof(ClientErrorOnlyFixtureController)]);

        offenders.Should().BeEmpty();
    }

    private static List<string> FindActionsWithUntypedSuccessResponse(IEnumerable<Type> candidateTypes)
    {
        List<string> offenders = [];

        IEnumerable<Type> controllers = candidateTypes
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract);

        foreach (Type controller in controllers)
        {
            if (!IsVisibleToApiExplorer(controller)
                || IsHiddenFromApiDescription(controller)
                || IsTestSupport(controller))
            {
                continue;
            }

            IEnumerable<MethodInfo> actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsRoutedAction);

            foreach (MethodInfo action in actions)
            {
                if (IsHiddenFromApiDescription(action) || !EmitsUntypedSuccessResponse(controller, action))
                {
                    continue;
                }

                offenders.Add($"{controller.Name}.{action.Name}");
            }
        }

        offenders.Sort(StringComparer.Ordinal);
        return offenders;
    }

    /// <summary>
    /// Accepts an inferred body type; otherwise checks declared success types.
    /// Without response attributes, treats the implicit 200 as untyped.
    /// </summary>
    private static bool EmitsUntypedSuccessResponse(Type controller, MethodInfo action)
    {
        List<ProducesResponseTypeAttribute> declared =
        [
            .. action.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true),
            .. controller.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true),
        ];

        if (IsTypedBody(GetInferredSuccessType(action.ReturnType)))
        {
            return false;
        }

        if (declared.Count == 0)
        {
            return true;
        }

        return declared.Exists(attribute =>
            IsBodyBearingSuccessCode(attribute.StatusCode) && !IsTypedBody(attribute.Type));
    }

    /// <summary>
    /// Applies this gate to 2xx responses except 204, matching the SDK document check.
    /// </summary>
    private static bool IsBodyBearingSuccessCode(int statusCode)
    {
        return statusCode is >= 200 and <= 299 && statusCode != StatusCodes.Status204NoContent;
    }

    /// <summary>
    /// Unwraps Task or ValueTask and extracts an ActionResult body type, if present.
    /// </summary>
    private static Type? GetInferredSuccessType(Type returnType)
    {
        Type unwrapped = returnType;

        if (unwrapped.IsGenericType
            && (unwrapped.GetGenericTypeDefinition() == typeof(Task<>)
                || unwrapped.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            unwrapped = unwrapped.GetGenericArguments()[0];
        }

        if (unwrapped.IsGenericType && unwrapped.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return unwrapped.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    /// Excludes void, object and result abstractions from accepted body types.
    /// </summary>
    private static bool IsTypedBody(Type? bodyType)
    {
        return bodyType is not null
            && bodyType != typeof(void)
            && bodyType != typeof(object)
            && !typeof(IActionResult).IsAssignableFrom(bodyType)
            && !typeof(IResult).IsAssignableFrom(bodyType);
    }

    private static bool IsRoutedAction(MethodInfo action)
    {
        return !action.IsSpecialName
            && action.GetCustomAttribute<NonActionAttribute>() is null
            && action.GetCustomAttributes().OfType<IActionHttpMethodProvider>().Any();
    }

    /// <summary>
    /// Selects controllers with ApiController or an explicit IgnoreApi=false setting.
    /// </summary>
    private static bool IsVisibleToApiExplorer(Type controller)
    {
        return controller.GetCustomAttribute<ApiControllerAttribute>() is not null
            || controller.GetCustomAttribute<ApiExplorerSettingsAttribute>()?.IgnoreApi == false;
    }

    private static bool IsHiddenFromApiDescription(MemberInfo member)
    {
        return member.GetCustomAttribute<ApiExplorerSettingsAttribute>()?.IgnoreApi == true;
    }

    private static bool IsTestSupport(Type controller)
    {
        TagsAttribute? tags = controller.GetCustomAttribute<TagsAttribute>();

        return tags is not null && tags.Tags.Contains(TestSupportTagName, StringComparer.Ordinal);
    }

    private static List<string> DiscoverModuleNames()
    {
        return Directory
            .GetFiles(AppContext.BaseDirectory, "Wallow.*.Domain.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!.Split('.')[1])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Named response contract for the reflection fixtures.
    /// </summary>
    private sealed record FixtureResponse(Guid Id);

    /// <summary>
    /// Declares an untyped 201 with no inferred body type.
    /// </summary>
    [ApiController]
    private sealed class BareCreatedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<IActionResult> Create() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>
    /// Declares a typed 200 alongside an untyped 202.
    /// </summary>
    [ApiController]
    private sealed class TypedOkWithBareAcceptedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(FixtureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task<IActionResult> Enqueue() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>
    /// Has neither response attributes nor an inferred body type.
    /// </summary>
    [ApiController]
    private sealed class NoResponseMetadataFixtureController : ControllerBase
    {
        [HttpGet]
        public Task<IActionResult> Get() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>
    /// Supplies an inferred body type for a bare 201.
    /// </summary>
    [ApiController]
    private sealed class InferredCreatedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<ActionResult<FixtureResponse>> Create() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    [ApiController]
    private sealed class TypedCreatedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(FixtureResponse), StatusCodes.Status201Created)]
        public Task<IActionResult> Create() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>
    /// Declares 204, which this gate excludes.
    /// </summary>
    [ApiController]
    private sealed class NoContentFixtureController : ControllerBase
    {
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public Task<IActionResult> Delete() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>
    /// Declares only 404, outside this success-response gate.
    /// </summary>
    [ApiController]
    private sealed class ClientErrorOnlyFixtureController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> Get() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }
}
