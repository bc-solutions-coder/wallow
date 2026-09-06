using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Factories;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// The emitted v1 document's <c>ErrorCode</c> enum is the aggregated catalog, nothing more and
/// nothing less. The committed snapshot in <c>packages/sdk/openapi/v1.json</c> is diffed against
/// the same document in CI, so this is what ties every registered code to the SDK's union type.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ErrorCodeCatalogExportTests(WallowApiFactory factory)
    : WallowIntegrationTestBase(factory)
{
    [Fact]
    public async Task ErrorCodeEnum_EqualsTheAggregatedCatalog()
    {
        ErrorCatalog catalog = ScopedServices.GetRequiredService<ErrorCatalog>();
        IOpenApiDocumentProvider documentProvider =
            ScopedServices.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(CancellationToken.None);

        OpenApiSchema errorCode = document.Components!.Schemas!["ErrorCode"]
            .Should().BeOfType<OpenApiSchema>().Subject;
        errorCode.Enum!.Select(node => node!.GetValue<string>())
            .Should().Equal(catalog.Entries.Select(entry => entry.Code));
    }

    [Fact]
    public void AggregatedCatalog_ContainsKernelAndModuleCodes()
    {
        ErrorCatalog catalog = ScopedServices.GetRequiredService<ErrorCatalog>();

        IEnumerable<string> codes = catalog.Entries.Select(entry => entry.Code);

        codes.Should().Contain(SharedErrors.ServerError.Code)
            .And.Contain(IdentityErrors.UserNotFound.Code);
    }

    [Fact]
    public async Task ProblemDetailsSchemas_ReferenceTheErrorCodeEnum()
    {
        IOpenApiDocumentProvider documentProvider =
            ScopedServices.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(CancellationToken.None);

        OpenApiSchema problemDetails = document.Components!.Schemas!["ProblemDetails"]
            .Should().BeOfType<OpenApiSchema>().Subject;
        problemDetails.Properties!["code"].Should().BeOfType<OpenApiSchemaReference>()
            .Which.Reference.Id.Should().Be("ErrorCode");
    }

    [Fact]
    public async Task EveryProblemDetailsSchema_ReferencesTheErrorCodeEnum()
    {
        IOpenApiDocumentProvider documentProvider =
            ScopedServices.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(CancellationToken.None);

        IEnumerable<string> problemSchemas = document.Components!.Schemas!.Keys
            .Where(name => name.EndsWith("ProblemDetails", StringComparison.Ordinal));
        problemSchemas.Should().NotBeEmpty();
        foreach (string schemaName in problemSchemas)
        {
            OpenApiSchema schema = document.Components.Schemas[schemaName]
                .Should().BeOfType<OpenApiSchema>().Subject;
            schema.Properties!["code"].Should().BeOfType<OpenApiSchemaReference>(
                    $"{schemaName} must carry the catalogued code")
                .Which.Reference.Id.Should().Be("ErrorCode");
        }
    }

    [Fact]
    public async Task EveryProblemDetailsSchema_RequiresCodeAndTraceIdAndHasNoInstance()
    {
        OpenApiDocument document = await GetDocumentAsync();

        foreach (string schemaName in ProblemSchemaNames(document))
        {
            OpenApiSchema schema = document.Components!.Schemas![schemaName]
                .Should().BeOfType<OpenApiSchema>().Subject;
            schema.Required.Should().Contain(ProblemContract.AlwaysPresentMembers, $"{schemaName} must require them");
            schema.Properties.Should().NotContainKey("instance", $"{schemaName} never carries instance");
            schema.Properties.Should().ContainKey(ProblemContract.TraceIdMember);
        }
    }

    [Fact]
    public async Task EveryOperation_DeclaresTheSharedFailureStatuses()
    {
        OpenApiDocument document = await GetDocumentAsync();

        foreach ((string path, HttpMethod method, OpenApiOperation operation) in Operations(document))
        {
            operation.Responses.Should().NotBeNull();
            operation.Responses!.Keys.Should().Contain(
                ProblemResponsesConvention.SharedStatusCodes.Select(status => status.ToString(CultureInfo.InvariantCulture)),
                $"{method} {path} must document every shared failure status");
        }
    }

    [Fact]
    public async Task OperationsAcceptingInput_Declare400AsAValidationProblem()
    {
        OpenApiDocument document = await GetDocumentAsync();

        foreach ((string path, HttpMethod method, OpenApiOperation operation) in Operations(document))
        {
            bool acceptsInput = operation.RequestBody is not null || operation.Parameters is { Count: > 0 };
            string expectedSchema = acceptsInput ? "HttpValidationProblemDetails" : "ProblemDetails";

            operation.Responses!["400"].Content.Should().ContainKey(
                ProblemContract.ContentType, $"{method} {path} declares a problem body for 400");
            IOpenApiSchema? schema = operation.Responses["400"].Content![ProblemContract.ContentType].Schema;
            schema.Should().BeOfType<OpenApiSchemaReference>($"{method} {path} declares a problem body for 400")
                .Which.Reference.Id.Should().Be(expectedSchema, $"{method} {path} accepts input: {acceptsInput}");
        }
    }

    private async Task<OpenApiDocument> GetDocumentAsync()
    {
        IOpenApiDocumentProvider documentProvider =
            ScopedServices.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        return await documentProvider.GetOpenApiDocumentAsync(CancellationToken.None);
    }

    private static List<string> ProblemSchemaNames(OpenApiDocument document)
    {
        List<string> names = document.Components!.Schemas!.Keys
            .Where(name => name.EndsWith("ProblemDetails", StringComparison.Ordinal))
            .ToList();
        names.Should().NotBeEmpty();
        return names;
    }

    private static List<(string Path, HttpMethod Method, OpenApiOperation Operation)> Operations(
        OpenApiDocument document)
    {
        List<(string Path, HttpMethod Method, OpenApiOperation Operation)> operations = [];
        foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths!)
        {
            foreach (KeyValuePair<HttpMethod, OpenApiOperation> operation in path.Value.Operations ?? [])
            {
                operations.Add((path.Key, operation.Key, operation.Value));
            }
        }

        operations.Should().NotBeEmpty();
        return operations;
    }
}
