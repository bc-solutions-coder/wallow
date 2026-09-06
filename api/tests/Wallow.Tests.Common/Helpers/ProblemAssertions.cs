using System.Net.Http.Json;
using System.Text.Json;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Tests.Common.Helpers;

/// <summary>
/// The wire-level assertion for the unified problem contract, shared by every integration sweep
/// so a route family cannot be swept to a weaker shape than the rest: the body is
/// <c>application/problem+json</c> and an object, <c>type</c> is blank, <c>title</c> is the
/// reason phrase, <c>status</c>, a catalogued <c>code</c> and a <c>traceId</c> are present,
/// <c>detail</c> is non-empty, <c>errors</c> appears only when asked for, and <c>instance</c>,
/// <c>api</c>, <c>version</c> and the exception member never do.
/// </summary>
public static class ProblemAssertions
{
    private static readonly string[] _neverPresent = ["instance", "api", "version", ProblemContract.ExceptionMember];

    /// <summary>
    /// Asserts the contract with the expected <paramref name="expectedCode"/>, checks the code is
    /// catalogued (the drift check: a code on the wire the catalog does not list is a bug wherever
    /// it was written), and returns the body for probe-specific assertions.
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
    /// The full contract for a catalogued entry, pinning <c>detail</c> to the entry's own
    /// user-safe sentence as well.
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
