using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Wallow.Shared.Kernel.Errors;
using ApiServiceCollectionExtensions = Wallow.Api.Extensions.ServiceCollectionExtensions;

namespace Wallow.Api.Tests.OpenApi;

/// <summary>
/// The catalog-to-document half of the error-code export: the aggregated catalog becomes one
/// string enum with a description per code, and every problem-details schema points its
/// <c>code</c> property at it.
/// </summary>
public class ErrorCodeSchemaTests
{
    private static readonly ErrorCatalog _catalog = ErrorCatalog.Aggregate([typeof(TestErrors)]);

    [Fact]
    public async Task TransformDocumentErrorCodes_EmitsStringEnumOfEveryCatalogCode()
    {
        OpenApiDocument document = new();

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        OpenApiSchema schema = GetErrorCodeSchema(document);
        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Enum.Should().NotBeNull();
        schema.Enum!.Select(node => node!.GetValue<string>())
            .Should().Equal(_catalog.Entries.Select(entry => entry.Code));
    }

    [Fact]
    public async Task TransformDocumentErrorCodes_DescribesEachCodeWithItsDefaultSentence()
    {
        OpenApiDocument document = new();

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        OpenApiSchema schema = GetErrorCodeSchema(document);
        schema.Extensions.Should().NotBeNull().And.ContainKey("x-enum-descriptions");
        JsonNodeExtension descriptions = schema.Extensions!["x-enum-descriptions"]
            .Should().BeOfType<JsonNodeExtension>().Subject;
        descriptions.Node.Should().BeOfType<JsonArray>()
            .Which.Select(node => node!.GetValue<string>())
            .Should().Equal(_catalog.Entries.Select(entry => entry.DefaultMessage));
    }

    [Fact]
    public async Task TransformDocumentErrorCodes_IncludesKernelAndModuleEntries()
    {
        OpenApiDocument document = new();

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        IEnumerable<string> codes = GetErrorCodeSchema(document).Enum!
            .Select(node => node!.GetValue<string>());
        codes.Should().Contain(SharedErrors.NotFound.Code)
            .And.Contain(TestErrors.WidgetNotFound.Code);
    }

    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("HttpValidationProblemDetails")]
    [InlineData("ValidationProblemDetails")]
    [InlineData("TenantProblemDetails")]
    public async Task TransformDocumentErrorCodes_PointsProblemDetailsCodeAtTheEnum(string schemaName)
    {
        OpenApiDocument document = new()
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    [schemaName] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            }
        };

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        OpenApiSchema problemDetails = document.Components!.Schemas![schemaName]
            .Should().BeOfType<OpenApiSchema>().Subject;
        problemDetails.Properties.Should().ContainKey("title", "existing members are preserved");
        OpenApiSchemaReference codeReference = problemDetails.Properties!["code"]
            .Should().BeOfType<OpenApiSchemaReference>().Subject;
        codeReference.Reference.Id.Should().Be(ApiServiceCollectionExtensions.ErrorCodeSchemaName);
    }

    [Fact]
    public async Task TransformDocumentErrorCodes_LeavesOtherSchemasAlone()
    {
        OpenApiSchema unrelated = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>()
        };
        OpenApiDocument document = new()
        {
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema> { ["InquiryResponse"] = unrelated }
            }
        };

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        unrelated.Properties.Should().BeEmpty();
        document.Components!.Schemas.Should().ContainKey(ApiServiceCollectionExtensions.ErrorCodeSchemaName);
    }

    private static OpenApiSchema GetErrorCodeSchema(OpenApiDocument document)
    {
        document.Components.Should().NotBeNull();
        document.Components!.Schemas.Should().ContainKey(ApiServiceCollectionExtensions.ErrorCodeSchemaName);
        return document.Components.Schemas![ApiServiceCollectionExtensions.ErrorCodeSchemaName]
            .Should().BeOfType<OpenApiSchema>().Subject;
    }

    private static class TestErrors
    {
        public static readonly ErrorCatalogEntry WidgetNotFound =
            new("Widget.NotFound", ErrorKind.NotFound, "Widget not found.");

        public static readonly ErrorCatalogEntry WidgetInUse =
            new("Widget.InUse", ErrorKind.BusinessRule, "The widget is still in use.");
    }
}
