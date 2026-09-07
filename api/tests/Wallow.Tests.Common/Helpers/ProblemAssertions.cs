using System.Net.Http.Json;
using System.Text.Json;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Tests.Common.Helpers;

/// <summary>
/// Asserts the shared HTTP problem shape and catalog membership.
/// </summary>
public static class ProblemAssertions
{
    private static readonly string[] _neverPresent = ["instance", "api", "version", ProblemContract.ExceptionMember];

    /// <summary>
    /// Checks the expected status/code, common problem fields and optional errors object.
    /// Returns the parsed body for additional assertions.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        this HttpResponseMessage response,
        ErrorCatalog catalog,
        int expectedStatus,
        string expectedCode,
        bool expectErrors = false)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(catalog);

        response.Content.Headers.ContentType.Should().NotBeNull();
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemContract.ContentType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Object);

        body.GetProperty("type").GetString().Should().Be(ProblemContract.BlankType);
        body.GetProperty("title").GetString().Should().Be(ProblemContract.TitleFor(expectedStatus));
        body.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        body.GetProperty(ProblemContract.CodeMember).GetString().Should().Be(expectedCode);
        body.GetProperty(ProblemContract.TraceIdMember).GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();

        catalog.Entries.Select(entry => entry.Code).Should().Contain(
            expectedCode, "every code on the wire must be catalogued");

        foreach (string member in _neverPresent)
        {
            body.TryGetProperty(member, out _).Should().BeFalse("{0} is not part of the contract", member);
        }

        body.TryGetProperty("errors", out JsonElement errors).Should().Be(expectErrors);
        if (expectErrors)
        {
            errors.ValueKind.Should().Be(JsonValueKind.Object);
        }

        return body;
    }

    /// <summary>
    /// Also checks that detail matches the catalog entry default message.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        this HttpResponseMessage response,
        ErrorCatalog catalog,
        int expectedStatus,
        ErrorCatalogEntry expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        JsonElement body = await response.AssertProblemAsync(catalog, expectedStatus, expected.Code);
        body.GetProperty("detail").GetString().Should().Be(expected.DefaultMessage);
        return body;
    }
}
