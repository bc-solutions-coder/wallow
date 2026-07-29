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
/// Guards the document-shape invariant that no operation emits a 200 response without a body
/// schema. An untyped 200 generates an SDK client method returning <c>unknown</c>, so every
/// action that can answer 200 must either return <c>ActionResult{T}</c> or declare
/// <c>[ProducesResponseType(typeof(T), StatusCodes.Status200OK)]</c>.
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

        List<string> offenders = FindActionsWithUntypedSuccessResponse(apiAssembly);

        offenders.Should().BeEmpty(
            $"every {moduleName} action that answers 200 must declare a typed body so the generated " +
            $"SDK client is typed rather than unknown. {offenders.Count} action(s) still emit an " +
            $"untyped 200: {string.Join(", ", offenders)}");
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

    private static List<string> FindActionsWithUntypedSuccessResponse(Assembly apiAssembly)
    {
        List<string> offenders = [];

        IEnumerable<Type> controllers = apiAssembly.GetTypes()
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
    /// Mirrors how <c>ApiResponseTypeProvider</c> builds the 200 entry: an explicit typed
    /// <c>[ProducesResponseType]</c> or an <c>ActionResult{T}</c> return type supplies the schema.
    /// Otherwise a 200 is still emitted — with no schema — whenever the action declares one
    /// explicitly, or declares no response metadata at all and so falls back to the default 200.
    /// A <c>[Produces]</c> content-type declaration alone never causes a 200 to be emitted: it
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

        bool declaresTyped200 = declared.Exists(
            attribute => attribute.StatusCode == StatusCodes.Status200OK && IsTypedBody(attribute.Type));

        if (declaresTyped200 || IsTypedBody(GetInferredSuccessType(action.ReturnType)))
        {
            return false;
        }

        bool declaresBare200 = declared.Exists(
            attribute => attribute.StatusCode == StatusCodes.Status200OK);

        return declaresBare200 || declared.Count == 0;
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
}
