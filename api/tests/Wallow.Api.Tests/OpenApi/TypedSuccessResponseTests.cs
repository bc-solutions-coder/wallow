using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Wallow.ServiceDefaults;

#pragma warning disable CA1024 // MemberData source methods cannot be properties

namespace Wallow.Api.Tests.OpenApi;

/// <summary>
/// Guards the document-shape invariant that no operation emits a body-bearing success response —
/// any 2xx other than 204 — without a body schema. An untyped success response generates an SDK
/// client method returning <c>unknown</c>, so every action that can answer such a code must either
/// return <c>ActionResult{T}</c> or declare <c>[ProducesResponseType(typeof(T), code)]</c>.
/// This is the reflection-level half of the rule; the SDK's <c>openapi-regen.test.ts</c> enforces
/// the same invariant against the generated document, and neither subsumes the other — this one
/// fails at build time on the offending action, that one on the regenerated snapshot.
/// </summary>
public class TypedSuccessResponseTests
{
    /// <summary>
    /// Tag marking internal-only endpoints, which a document transformer strips from the public
    /// v1 document before it ever reaches the SDK generator.
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
    /// The shape <c>InvitationsController.Create</c> uses: ApiExplorer back-fills the inferred
    /// <c>ActionResult{T}</c> body into a bare 2xx entry, so the document schema is present and the
    /// action is not an offender. Widening the gate past 200 must not regress into flagging this.
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
    /// Mirrors how <c>ApiResponseTypeProvider</c> builds each body-bearing success entry: an explicit
    /// typed <c>[ProducesResponseType]</c> supplies that entry's schema, and an <c>ActionResult{T}</c>
    /// return type is back-filled into every declared 2xx entry that names no type of its own — which
    /// is why a bare <c>[ProducesResponseType(201)]</c> on such an action still reaches the document
    /// with a schema. Absent both, the response is emitted with no schema. An action that declares no
    /// response metadata at all falls back to the implicit default 200, and that fallback alone is
    /// 200-specific: declaring any response — even a 404 — suppresses it.
    /// A <c>[Produces]</c> content-type declaration alone never causes a response to be emitted: it
    /// only sets the content-type of whatever responses are already declared, and is not itself
    /// an <c>IApiResponseMetadataProvider</c> entry.
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
    /// A 2xx that can carry a body, and so must name a schema. 204 is excluded because it has no body
    /// by definition. Mirrors <c>isBodyBearingSuccessCode</c> in the SDK's document-level invariant
    /// (<c>packages/sdk/src/openapi-regen.test.ts</c>), which guards the same rule one layer down.
    /// </summary>
    private static bool IsBodyBearingSuccessCode(int statusCode)
    {
        return statusCode is >= 200 and <= 299 && statusCode != StatusCodes.Status204NoContent;
    }

    /// <summary>
    /// Returns the body type ASP.NET Core infers from the action's return type, or <see langword="null"/>
    /// when the action returns a non-generic result and therefore carries no inferable schema.
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
    /// A body type only produces a schema when it names a real contract. <see cref="object"/> and the
    /// result abstractions serialize to an empty schema, which is exactly the untyped case this guards.
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
    /// Only <c>[ApiController]</c> controllers get <c>ApiExplorer.IsVisible</c> turned on, so the OIDC
    /// controllers — which carry the plain <c>[Controller]</c> attribute — never reach the OpenAPI
    /// document and cannot contribute an untyped 200 to it.
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
    /// Body contract for the fixture controllers below. Any named type satisfies
    /// <see cref="IsTypedBody"/>; the members exist only so it is a plausible response.
    /// </summary>
    private sealed record FixtureResponse(Guid Id);

    /// <summary>
    /// The defect shape this gate exists to catch: a bare non-200 success code on an action whose
    /// return type carries nothing for ApiExplorer to infer, so the document emits a schemaless 201.
    /// This is exactly the untyped 201 that Wallow-td30 had to fix on POST /v1/inquiries/{id}/comments.
    /// </summary>
    [ApiController]
    private sealed class BareCreatedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<IActionResult> Create() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>An untyped success code hiding behind a correctly typed 200 on the same action.</summary>
    [ApiController]
    private sealed class TypedOkWithBareAcceptedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(FixtureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public Task<IActionResult> Enqueue() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>No response metadata at all, so ApiExplorer falls back to a schemaless default 200.</summary>
    [ApiController]
    private sealed class NoResponseMetadataFixtureController : ControllerBase
    {
        [HttpGet]
        public Task<IActionResult> Get() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>A bare 201 whose schema ApiExplorer back-fills from <c>ActionResult{T}</c>.</summary>
    [ApiController]
    private sealed class InferredCreatedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public Task<ActionResult<FixtureResponse>> Create() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>A 201 that names its body outright.</summary>
    [ApiController]
    private sealed class TypedCreatedFixtureController : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(FixtureResponse), StatusCodes.Status201Created)]
        public Task<IActionResult> Create() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>204 carries no body by definition, so a bare declaration is correct rather than untyped.</summary>
    [ApiController]
    private sealed class NoContentFixtureController : ControllerBase
    {
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public Task<IActionResult> Delete() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }

    /// <summary>
    /// Only a non-success code is declared. It needs no schema of its own, and declaring any response
    /// metadata suppresses ApiExplorer's implicit 200, so nothing schemaless reaches the document.
    /// </summary>
    [ApiController]
    private sealed class ClientErrorOnlyFixtureController : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> Get() => throw new NotSupportedException("Reflection fixture; never invoked.");
    }
}
