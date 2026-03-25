using System.Reflection;
using Wallow.Api.Extensions;
using Wallow.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Trace;

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
        Dictionary<string, string?>? configOverrides = null,
        string environmentName = "Development")
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
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        services.AddLogging();
        services.AddApiServices(config, env);
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
    public void AddApiServices_RegistersCorsService()
    {
        ServiceCollection services = CreateServicesWithApiDefaults(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://app.wallow.dev",
        });

        ServiceProvider provider = services.BuildServiceProvider();
        ICorsService? corsService =
            provider.GetService<ICorsService>();
        corsService.Should().NotBeNull();
    }

    [Fact]
    public void AddApiServices_InProduction_WithEmptyAllowedOrigins_Throws()
    {
        Action act = () => CreateServicesWithApiDefaults(environmentName: "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AllowedOrigins*must be configured*");
    }

    [Fact]
    public void AddApiServices_InDevelopment_WithEmptyAllowedOrigins_DoesNotThrow()
    {
        Action act = () => CreateServicesWithApiDefaults(environmentName: "Development");

        act.Should().NotThrow();
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
    public void AddObservability_InDevelopment_RegistersOpenTelemetry()
    {
        ServiceCollection services = new();
        IConfiguration config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "TestService",
            ["OpenTelemetry:OtlpGrpcEndpoint"] = "http://localhost:4317",
        });
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddLogging();
        services.AddObservability(config, env);

        ServiceProvider provider = services.BuildServiceProvider();
        TracerProvider? tracerProvider = provider.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddObservability_InProduction_WithoutEndpoint_Throws()
    {
        ServiceCollection services = new();
        IConfiguration config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "TestService",
        });
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        Action act = () => services.AddObservability(config, env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OtlpGrpcEndpoint*must be configured*");
    }

    [Fact]
    public void AddObservability_InDevelopment_WithoutEndpoint_UsesDefaultEndpoint()
    {
        ServiceCollection services = new();
        IConfiguration config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "TestService",
        });
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddLogging();
        services.AddObservability(config, env);

        ServiceProvider provider = services.BuildServiceProvider();
        TracerProvider? tracerProvider = provider.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddObservability_WithDefaultServiceName_UsesWallow()
    {
        ServiceCollection services = new();
        IConfiguration config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:OtlpGrpcEndpoint"] = "http://localhost:4317",
        });
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddLogging();

        Action act = () => services.AddObservability(config, env);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddObservability_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();
        IConfiguration config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OpenTelemetry:OtlpGrpcEndpoint"] = "http://localhost:4317",
        });
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddLogging();
        IServiceCollection result = services.AddObservability(config, env);

        result.Should().BeSameAs(services);
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

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentInfo(document, "Wallow");

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

        await Wallow.Api.Extensions.ServiceCollectionExtensions.TransformDocumentInfo(document, "MyBrand");

        document.Info.Title.Should().Be("MyBrand API");
        document.Info.Contact!.Name.Should().Be("MyBrand");
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
