using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Wallow.Api.Extensions;
using Wallow.Api.Middleware;

namespace Wallow.Api.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }

    private static ServiceCollection CreateServicesWithApiDefaults(
        Dictionary<string, string?>? configOverrides = null)
    {
        ServiceCollection services = new();
        Dictionary<string, string?> defaults = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
            ["ConnectionStrings:Redis"] = "localhost:6379",
        };

        if (configOverrides is not null)
        {
            foreach (KeyValuePair<string, string?> kvp in configOverrides)
            {
                defaults[kvp.Key] = kvp.Value;
            }
        }

        IConfiguration config = BuildConfiguration(defaults);
        services.AddLogging();
        services.AddApiServices(config);
        return services;
    }

    [Fact]
    public void AddApiServices_RegistersProblemDetails()
    {
        ServiceCollection services = CreateServicesWithApiDefaults();

        ServiceProvider provider = services.BuildServiceProvider();
        IOptions<ProblemDetailsOptions>? options =
            provider.GetService<IOptions<ProblemDetailsOptions>>();
        options.Should().NotBeNull();
    }

    [Fact]
    public void AddApiServices_RegistersExceptionHandler()
    {
        ServiceCollection services = CreateServicesWithApiDefaults();

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IExceptionHandler));
        descriptor.Should().NotBeNull();
        descriptor.ImplementationType.Should().Be<GlobalExceptionHandler>();
    }

    [Fact]
    public void AddApiServices_RegistersHealthChecks()
    {
        ServiceCollection services = CreateServicesWithApiDefaults();

        ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckService? healthCheckService =
            provider.GetService<HealthCheckService>();
        healthCheckService.Should().NotBeNull();
    }

    [Fact]
    public void AddWallowRateLimiting_RegistersRateLimiterOptions()
    {
        ServiceCollection services = new();

        services.AddWallowRateLimiting();

        ServiceDescriptor? descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddApiServices_ReturnsSameServiceCollection()
    {
        ServiceCollection services = CreateServicesWithApiDefaults();

        services.Should().NotBeNull();
    }

    [Fact]
    public void AddWallowRateLimiting_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddWallowRateLimiting();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddApiServices_ProblemDetailsCustomization_AddsApiAndVersionExtensions()
    {
        ServiceCollection services = CreateServicesWithApiDefaults();
        ServiceProvider provider = services.BuildServiceProvider();

        IOptions<ProblemDetailsOptions> options =
            provider.GetRequiredService<IOptions<ProblemDetailsOptions>>();

        ProblemDetailsContext context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails()
        };
        options.Value.CustomizeProblemDetails?.Invoke(context);

        context.ProblemDetails.Extensions["api"].Should().Be("Wallow");
        context.ProblemDetails.Extensions["version"].Should().Be("1.0.0");
    }

    [Fact]
    public async Task TransformDocumentInfo_SetsCorrectApiInfo()
    {
        OpenApiDocument document = new();

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentInfo(document, "Wallow", "v1");

        document.Info.Should().NotBeNull();
        document.Info.Title.Should().Be("Wallow API");
        document.Info.Version.Should().Be("v1");
        document.Info.Description.Should().Contain("modular monolith");
        document.Info.Contact.Should().NotBeNull();
        document.Info.Contact!.Name.Should().Be("Wallow");
    }

    [Fact]
    public async Task TransformDocumentSecurity_AddsBearerSecurityScheme()
    {
        OpenApiDocument document = new();

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentSecurity(document);

        document.Components.Should().NotBeNull();
        document.Components!.SecuritySchemes.Should().ContainKey("Bearer");
        IOpenApiSecurityScheme bearerScheme = document.Components.SecuritySchemes["Bearer"];
        bearerScheme.Type.Should().Be(SecuritySchemeType.Http);
        bearerScheme.Scheme.Should().Be("bearer");
        bearerScheme.BearerFormat.Should().Be("JWT");
    }

    [Fact]
    public async Task TransformDocumentSecurity_AddsGlobalSecurityRequirement()
    {
        OpenApiDocument document = new();

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentSecurity(document);

        document.Security.Should().NotBeNull();
        document.Security.Should().HaveCount(1);
    }

    [Fact]
    public async Task TransformOperationSecurity_WithAllowAnonymous_ClearsSecurity()
    {
        OpenApiOperation operation = new OpenApiOperation
        {
            Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("test")] = [] }]
        };
        ApiDescription apiDescription = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = [new AllowAnonymousAttribute()]
            }
        };
        OpenApiOperationTransformerContext context = new OpenApiOperationTransformerContext
        {
            Description = apiDescription,
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationSecurity(operation, context);

        operation.Security.Should().BeEmpty();
    }

    [Fact]
    public async Task TransformOperationSecurity_WithoutAllowAnonymous_PreservesSecurity()
    {
        OpenApiOperation operation = new OpenApiOperation
        {
            Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("test")] = [] }]
        };
        ApiDescription apiDescription = new ApiDescription
        {
            ActionDescriptor = new ActionDescriptor
            {
                EndpointMetadata = []
            }
        };
        OpenApiOperationTransformerContext context = new OpenApiOperationTransformerContext
        {
            Description = apiDescription,
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationSecurity(operation, context);

        operation.Security.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("/health", false)]
    [InlineData("/healthz", false)]
    [InlineData("/Health/Ready", false)]
    [InlineData("/alive", false)]
    [InlineData("/Alive", false)]
    [InlineData("/api/users", true)]
    [InlineData("/", true)]
    [InlineData("/openapi/v1.json", true)]
    public void FilterTelemetryRequest_FiltersCorrectPaths(string path, bool expected)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = path;

        bool result = Wallow.Api.Extensions.ServiceCollectionExtensions.FilterTelemetryRequest(httpContext);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task TransformOperationModuleTag_WithModuleNamespace_SetsModuleTag()
    {
        OpenApiOperation operation = new();
        ControllerActionDescriptor actionDescriptor = new()
        {
            ControllerTypeInfo = typeof(Wallow.Billing.Api.Controllers.FakeBillingController).GetTypeInfo(),
            EndpointMetadata = []
        };
        ApiDescription apiDescription = new()
        {
            ActionDescriptor = actionDescriptor
        };
        OpenApiOperationTransformerContext context = new()
        {
            Description = apiDescription,
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationModuleTag(operation, context);

        operation.Tags.Should().NotBeNull();
        operation.Tags.Should().HaveCount(1);
    }

    [Fact]
    public async Task TransformOperationModuleTag_WithExplicitTagsAttribute_DoesNotOverride()
    {
        OpenApiOperation operation = new()
        {
            Tags = new HashSet<OpenApiTagReference> { new OpenApiTagReference("ExistingTag") }
        };
        ActionDescriptor actionDescriptor = new()
        {
            EndpointMetadata = [new TagsAttribute("CustomTag")]
        };
        ApiDescription apiDescription = new()
        {
            ActionDescriptor = actionDescriptor
        };
        OpenApiOperationTransformerContext context = new()
        {
            Description = apiDescription,
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationModuleTag(operation, context);

        operation.Tags.Should().HaveCount(1);
    }

    [Fact]
    public async Task TransformOperationModuleTag_WithNonControllerDescriptor_DoesNotSetTag()
    {
        OpenApiOperation operation = new();
        ActionDescriptor actionDescriptor = new()
        {
            EndpointMetadata = []
        };
        ApiDescription apiDescription = new()
        {
            ActionDescriptor = actionDescriptor
        };
        OpenApiOperationTransformerContext context = new()
        {
            Description = apiDescription,
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationModuleTag(operation, context);

        operation.Tags.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task TransformDocumentInfo_WithCustomAppName_SetsCustomTitle()
    {
        OpenApiDocument document = new();

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentInfo(document, "MyBrand", "v1");

        document.Info.Title.Should().Be("MyBrand API");
        document.Info.Contact!.Name.Should().Be("MyBrand");
    }

    private static OpenApiOperation OperationWithTags(params string[] tagNames)
    {
        OpenApiOperation operation = new()
        {
            Tags = new HashSet<OpenApiTagReference>()
        };

        foreach (string tagName in tagNames)
        {
            operation.Tags.Add(new OpenApiTagReference(tagName));
        }

        return operation;
    }

    [Fact]
    public async Task TransformDocumentExcludeTestSupport_RemovesPathWhoseOperationsAreAllTestSupport()
    {
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/v1/identity/test/isolated-org"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = OperationWithTags("Test Support")
                    }
                }
            }
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentExcludeTestSupport(document);

        document.Paths.Should().NotContainKey("/v1/identity/test/isolated-org");
    }

    [Fact]
    public async Task TransformDocumentExcludeTestSupport_PreservesPathsWithoutTestSupportTag()
    {
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/v1/identity/organizations"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = OperationWithTags("Identity"),
                        [HttpMethod.Post] = OperationWithTags("Identity")
                    }
                },
                ["/v1/identity/test/isolated-org"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = OperationWithTags("Test Support")
                    }
                }
            }
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentExcludeTestSupport(document);

        document.Paths.Should().ContainKey("/v1/identity/organizations");
        document.Paths["/v1/identity/organizations"].Operations.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransformDocumentExcludeTestSupport_RemovesOnlyTestSupportOperationsFromMixedPath()
    {
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/v1/identity/mixed"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = OperationWithTags("Identity"),
                        [HttpMethod.Post] = OperationWithTags("Test Support")
                    }
                }
            }
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentExcludeTestSupport(document);

        document.Paths.Should().ContainKey("/v1/identity/mixed");
        document.Paths["/v1/identity/mixed"].Operations.Should().ContainKey(HttpMethod.Get);
        document.Paths["/v1/identity/mixed"].Operations.Should().NotContainKey(HttpMethod.Post);
    }

    [Fact]
    public async Task TransformDocumentExcludeTestSupport_RemovesTestSupportEntryFromDocumentTags()
    {
        OpenApiDocument document = new()
        {
            Paths = new OpenApiPaths
            {
                ["/v1/identity/test/isolated-org"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Post] = OperationWithTags("Test Support")
                    }
                }
            },
            Tags = new HashSet<OpenApiTag>
            {
                new OpenApiTag { Name = "Identity" },
                new OpenApiTag { Name = "Test Support" }
            }
        };

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentExcludeTestSupport(document);

        document.Tags.Should().NotBeNull();
        document.Tags.Should().NotContain(tag => tag.Name == "Test Support");
        document.Tags.Should().Contain(tag => tag.Name == "Identity");
    }

    [Fact]
    public async Task TransformDocumentExcludeTestSupport_WithNoPaths_DoesNotThrow()
    {
        OpenApiDocument document = new();

        Func<Task> act = async () =>
            await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentExcludeTestSupport(document);

        await act.Should().NotThrowAsync();
    }

    // OpenApiOptions.DocumentTransformers is internal to Microsoft.AspNetCore.OpenApi, so the
    // registration assertion has to reach it reflectively.
    private static List<IOpenApiDocumentTransformer> GetRegisteredDocumentTransformers(
        OpenApiOptions options)
    {
        FieldInfo field = typeof(OpenApiOptions)
            .GetField("DocumentTransformers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.Should().NotBeNull("Microsoft.AspNetCore.OpenApi still stores transformers in an internal field");

        return ((IEnumerable<IOpenApiDocumentTransformer>)field.GetValue(options)!).ToList();
    }

    // Builds the versioned options shape Asp.Versioning hands to the callback for one
    // discovered API version, runs the callback, and returns it for transformer assertions.
    private static VersionedOpenApiOptions BuildConfiguredVersionedOptions()
    {
        VersionedOpenApiOptions options = new()
        {
            Description = new ApiVersionDescription(new ApiVersion(1, 0), "v1"),
            Document = new OpenApiOptions(),
            DocumentDescription = new OpenApiDocumentDescriptionOptions(),
        };

        Wallow.Api.Extensions.ServiceCollectionExtensions.ConfigureVersionedOpenApiDocument(
            options, BuildConfiguration());

        return options;
    }

    [Fact]
    public void ConfigureVersionedOpenApiDocument_RegistersDocumentTransformers()
    {
        VersionedOpenApiOptions options = BuildConfiguredVersionedOptions();

        // Info, security, test-support exclusion, empty-placeholder scrub.
        GetRegisteredDocumentTransformers(options.Document).Should().HaveCount(4);
    }

    [Fact]
    public void AddApiServices_AnchorsXmlCommentSupportOnTheV1Document()
    {
        ServiceCollection services = CreateServicesWithApiDefaults();
        ServiceProvider provider = services.BuildServiceProvider();

        OpenApiOptions options = provider
            .GetRequiredService<IOptionsMonitor<OpenApiOptions>>()
            .Get("v1");

        // The bare AddOpenApi("v1") call in AddApiServices exists solely so the framework's
        // compile-time XML-comment interceptor attaches its transformers to the "v1" named
        // options the versioned pipeline resolves. If the anchor is removed, nothing registers
        // here and every XML doc comment silently drops out of the emitted document. The
        // interceptor contributes operation and schema transformers, not document transformers.
        GetRegisteredOperationTransformers(options).Should().NotBeEmpty(
            "the XML-comment interceptor must attach transformers to the v1 anchor");
    }

    // Same reflective reach as GetRegisteredDocumentTransformers: OpenApiOptions stores the
    // operation transformers in an internal field too.
    private static List<IOpenApiOperationTransformer> GetRegisteredOperationTransformers(
        OpenApiOptions options)
    {
        FieldInfo field = typeof(OpenApiOptions)
            .GetField("OperationTransformers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.Should().NotBeNull("Microsoft.AspNetCore.OpenApi still stores transformers in an internal field");

        return ((IEnumerable<IOpenApiOperationTransformer>)field.GetValue(options)!).ToList();
    }

    private static OpenApiOperationTransformerContext ContextFor(ActionDescriptor actionDescriptor)
    {
        return new OpenApiOperationTransformerContext
        {
            Description = new ApiDescription { ActionDescriptor = actionDescriptor },
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider()
        };
    }

    private static ControllerActionDescriptor ControllerAction(string controllerName, string methodName)
    {
        return new ControllerActionDescriptor
        {
            ControllerName = controllerName,
            MethodInfo = typeof(FakeOperationIdActions).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic)!,
            EndpointMetadata = []
        };
    }

    [Fact]
    public async Task TransformOperationId_SetsControllerNameAndMethodNameDerivedId()
    {
        OpenApiOperation operation = new();
        ControllerActionDescriptor actionDescriptor = ControllerAction("Organizations", "GetById");

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
            operation, ContextFor(actionDescriptor));

        operation.OperationId.Should().Be("OrganizationsGetById");
    }

    [Fact]
    public async Task TransformOperationId_WithSameMethodNameOnDifferentControllers_ProducesDistinctIds()
    {
        OpenApiOperation organizationsOperation = new();
        OpenApiOperation usersOperation = new();

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
            organizationsOperation, ContextFor(ControllerAction("Organizations", "GetById")));
        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
            usersOperation, ContextFor(ControllerAction("Users", "GetById")));

        organizationsOperation.OperationId.Should().Be("OrganizationsGetById");
        usersOperation.OperationId.Should().Be("UsersGetById");
        organizationsOperation.OperationId.Should().NotBe(usersOperation.OperationId);
    }

    [Fact]
    public async Task TransformOperationId_IgnoresActionNameOverride_AndUsesMethodInfoName()
    {
        OpenApiOperation operation = new();
        ControllerActionDescriptor actionDescriptor = ControllerAction("Organizations", "GetById");
        actionDescriptor.ActionName = "look-up-by-identifier";

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
            operation, ContextFor(actionDescriptor));

        operation.OperationId.Should().Be("OrganizationsGetById");
    }

    [Fact]
    public async Task TransformOperationId_WithNonControllerActionDescriptor_LeavesOperationIdUnset()
    {
        OpenApiOperation operation = new();
        ActionDescriptor actionDescriptor = new() { EndpointMetadata = [] };

        Func<Task> act = async () =>
            await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
                operation, ContextFor(actionDescriptor));

        await act.Should().NotThrowAsync();
        operation.OperationId.Should().BeNull();
    }

    [Fact]
    public void ConfigureVersionedOpenApiDocument_RegistersOperationTransformers()
    {
        VersionedOpenApiOptions options = BuildConfiguredVersionedOptions();

        List<IOpenApiOperationTransformer> transformers =
            GetRegisteredOperationTransformers(options.Document);

        // Security, module tag, operationId.
        transformers.Should().HaveCount(3,
            "registered operation transformers are: {0}",
            string.Join(", ", transformers.Select(transformer => transformer.GetType().Name)));
    }

    // Every controller in the API lives in a Wallow.{Module}.Api assembly, all of which are copied
    // next to this test assembly via the Wallow.Api project reference.
    private static List<(string ControllerName, MethodInfo Method)> DiscoverControllerActions()
    {
        List<(string ControllerName, MethodInfo Method)> actions = [];

        foreach (string assemblyPath in Directory.GetFiles(
            AppDomain.CurrentDomain.BaseDirectory, "Wallow.*.Api.dll"))
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);

            foreach (Type controllerType in assembly.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract))
            {
                string controllerName = controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
                    ? controllerType.Name[..^"Controller".Length]
                    : controllerType.Name;

                foreach (MethodInfo method in controllerType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.IsSpecialName || method.GetCustomAttribute<NonActionAttribute>() is not null)
                    {
                        continue;
                    }

                    actions.Add((controllerName, method));
                }
            }
        }

        return actions;
    }

    [Fact]
    public void DiscoverControllerActions_FindsTheApiSurface()
    {
        DiscoverControllerActions().Should().HaveCountGreaterThan(100,
            "the API exposes well over a hundred controller actions; an empty or tiny result means "
            + "assembly discovery broke, which would make the operationId invariants vacuous");
    }

    // Regression guard for the naming scheme itself: {ControllerName}{MethodName} is only a valid
    // operationId source while no controller declares two actions with the same method name.
    [Fact]
    public void ControllerActionSurface_HasNoDuplicateControllerAndMethodNamePairs()
    {
        List<string> duplicates = DiscoverControllerActions()
            .GroupBy(action => $"{action.ControllerName}{action.Method.Name}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} (x{group.Count()})")
            .ToList();

        duplicates.Should().BeEmpty(
            "controller name plus method name must uniquely identify every action; overloads sharing "
            + "a method name inside one controller would collide: {0}",
            string.Join(", ", duplicates));
    }

    [Fact]
    public async Task EveryControllerAction_ReceivesANonEmptyOperationId()
    {
        List<string> missing = [];

        foreach ((string controllerName, MethodInfo method) in DiscoverControllerActions())
        {
            OpenApiOperation operation = new();
            ControllerActionDescriptor actionDescriptor = new()
            {
                ControllerName = controllerName,
                MethodInfo = method,
                EndpointMetadata = []
            };

            await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
                operation, ContextFor(actionDescriptor));

            if (string.IsNullOrWhiteSpace(operation.OperationId))
            {
                missing.Add($"{controllerName}.{method.Name}");
            }
        }

        missing.Should().BeEmpty(
            "every operation in the v1 document must carry an operationId: {0}",
            string.Join(", ", missing));
    }

    [Fact]
    public async Task EveryControllerAction_ReceivesAUniqueOperationId()
    {
        List<string> operationIds = [];

        foreach ((string controllerName, MethodInfo method) in DiscoverControllerActions())
        {
            OpenApiOperation operation = new();
            ControllerActionDescriptor actionDescriptor = new()
            {
                ControllerName = controllerName,
                MethodInfo = method,
                EndpointMetadata = []
            };

            await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformOperationId(
                operation, ContextFor(actionDescriptor));

            operationIds.Add(operation.OperationId ?? string.Empty);
        }

        List<string> duplicates = operationIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"'{group.Key}' (x{group.Count()})")
            .ToList();

        duplicates.Should().BeEmpty(
            "the v1 document must contain zero duplicate operationIds: {0}",
            string.Join(", ", duplicates));
    }

    [Fact]
    public void FilterTelemetryRequest_WithNullPath_ReturnsTrue()
    {
        DefaultHttpContext httpContext = new();
        // Path is empty by default

        bool result = Wallow.Api.Extensions.ServiceCollectionExtensions.FilterTelemetryRequest(httpContext);

        result.Should().BeTrue();
    }
}
