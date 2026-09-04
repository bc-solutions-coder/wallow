using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Tests.Errors;

public class SharedErrorsTests
{
    [Fact]
    public void Catalog_HoldsExactlyTheNineStatusGenericEntries()
    {
        IReadOnlyList<ErrorCatalogEntry> entries = ErrorCatalog.EntriesOf(typeof(SharedErrors));

        entries.Select(entry => (entry.Code, entry.Kind)).Should().BeEquivalentTo(
        [
            ("Validation.Failed", ErrorKind.Validation),
            ("Auth.Unauthenticated", ErrorKind.Unauthenticated),
            ("Auth.Forbidden", ErrorKind.Forbidden),
            ("Http.NotFound", ErrorKind.NotFound),
            ("Http.MethodNotAllowed", ErrorKind.MethodNotAllowed),
            ("RateLimit.Exceeded", ErrorKind.RateLimited),
            ("Setup.Required", ErrorKind.Unavailable),
            ("Http.ClientError", ErrorKind.Validation),
            ("Server.Error", ErrorKind.Failure),
        ]);
    }

    [Fact]
    public void EveryEntry_HasAUserSafeDefaultSentence()
    {
        foreach (ErrorCatalogEntry entry in ErrorCatalog.EntriesOf(typeof(SharedErrors)))
        {
            entry.DefaultMessage.Should().EndWith(".", because: $"{entry.Code} should read as a sentence");
        }
    }
}
