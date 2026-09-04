using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Wallow.Shared.Api.Problems;
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

    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("HttpValidationProblemDetails")]
    public async Task TransformDocumentErrorCodes_RequiresTheAlwaysPresentMembers(string schemaName)
    {
        OpenApiDocument document = DocumentWithProblemSchema(schemaName);

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        OpenApiSchema problemDetails = document.Components!.Schemas![schemaName]
            .Should().BeOfType<OpenApiSchema>().Subject;
        problemDetails.Required.Should().BeEquivalentTo(ProblemContract.AlwaysPresentMembers);
        problemDetails.Properties![ProblemContract.TraceIdMember]
            .Should().BeOfType<OpenApiSchema>().Which.Type.Should().Be(JsonSchemaType.String);
    }

    [Fact]
    public async Task TransformDocumentErrorCodes_DropsTheInstanceMember()
    {
        OpenApiDocument document = DocumentWithProblemSchema("ProblemDetails");

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        OpenApiSchema problemDetails = document.Components!.Schemas!["ProblemDetails"]
            .Should().BeOfType<OpenApiSchema>().Subject;
        problemDetails.Properties.Should().NotContainKey("instance");
        problemDetails.Properties.Should().ContainKey("title", "the other standard members stay");
    }

    [Fact]
    public async Task TransformDocumentErrorCodes_IsIdempotent()
    {
        OpenApiDocument document = DocumentWithProblemSchema("ProblemDetails");

        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);
        await ApiServiceCollectionExtensions.TransformDocumentErrorCodes(document, _catalog);

        OpenApiSchema problemDetails = document.Components!.Schemas!["ProblemDetails"]
            .Should().BeOfType<OpenApiSchema>().Subject;
        problemDetails.Required.Should().HaveCount(ProblemContract.AlwaysPresentMembers.Count);
    }

    [Fact]
    public async Task TransformDocumentProblemContentTypes_ServesProblemBodiesAsProblemJson()
    {
        OpenApiDocument document = DocumentWithProblemSchema("ProblemDetails");
        OpenApiMediaType problemBody = new() { Schema = new OpenApiSchemaReference("ProblemDetails", document) };
        OpenApiMediaType okBody = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } };
        OpenApiOperation operation = new()
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = okBody }
                },
                ["404"] = new OpenApiResponse
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = problemBody,
                        ["text/json"] = problemBody,
                        ["text/plain"] = problemBody
                    }
                }
            }
        };
        document.Paths = new OpenApiPaths
        {
            ["/things"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = operation }
            }
        };

        await ApiServiceCollectionExtensions.TransformDocumentProblemContentTypes(document);

        operation.Responses["404"].Content.Should().HaveCount(1)
            .And.ContainKey(ProblemContract.ContentType).WhoseValue.Should().BeSameAs(problemBody);
        operation.Responses["200"].Content.Should().ContainKey("application/json", "successes are untouched");
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

    private static OpenApiDocument DocumentWithProblemSchema(string schemaName) => new()
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
                        ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            }
        }
    };

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
