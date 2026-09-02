using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Tests.Errors;

public class ErrorCatalogTests
{
    [Fact]
    public void EntriesOf_ReadsPublicStaticFieldsAndProperties()
    {
        IReadOnlyList<ErrorCatalogEntry> entries = ErrorCatalog.EntriesOf(typeof(FakeModuleErrors));

        entries.Select(entry => entry.Code).Should().BeEquivalentTo(
            ["Fake.FieldEntry", "Fake.PropertyEntry"]);
    }

    [Fact]
    public void EntriesOf_IgnoresNonEntryMembers()
    {
        IReadOnlyList<ErrorCatalogEntry> entries = ErrorCatalog.EntriesOf(typeof(FakeModuleErrors));

        entries.Should().HaveCount(2);
    }

    [Fact]
    public void EntriesOf_RejectsATypeWithNoEntries()
    {
        Func<IReadOnlyList<ErrorCatalogEntry>> act = () => ErrorCatalog.EntriesOf(typeof(NotACatalog));

        act.Should().Throw<ArgumentException>().WithMessage("*not an error catalog*");
    }

    [Fact]
    public void Aggregate_AlwaysIncludesSharedErrors()
    {
        ErrorCatalog catalog = ErrorCatalog.Aggregate([]);

        catalog.Entries.Should().BeEquivalentTo(ErrorCatalog.EntriesOf(typeof(SharedErrors)));
    }

    [Fact]
    public void Aggregate_MergesModuleCatalogsAndSortsByCode()
    {
        ErrorCatalog catalog = ErrorCatalog.Aggregate([typeof(FakeModuleErrors), typeof(OtherModuleErrors)]);

        List<string> codes = catalog.Entries.Select(entry => entry.Code).ToList();

        codes.Should().Contain(["Fake.FieldEntry", "Fake.PropertyEntry", "Other.Entry", "Http.NotFound"]);
        codes.Should().BeInAscendingOrder(StringComparer.Ordinal);
        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Aggregate_CountsARepeatedTypeOnce()
    {
        ErrorCatalog catalog = ErrorCatalog.Aggregate([typeof(FakeModuleErrors), typeof(FakeModuleErrors), typeof(SharedErrors)]);

        catalog.Entries.Should().HaveCount(8 + 2);
    }

    [Fact]
    public void Aggregate_RefusesACodeOwnedByTwoCatalogs()
    {
        Func<ErrorCatalog> act = () => ErrorCatalog.Aggregate([typeof(FakeModuleErrors), typeof(DuplicatingErrors)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Fake.FieldEntry*exactly one owner*");
    }

    [Fact]
    public void Aggregate_RefusesACatalogRedeclaringASharedCode()
    {
        Func<ErrorCatalog> act = () => ErrorCatalog.Aggregate([typeof(RedeclaresSharedErrors)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Http.NotFound*");
    }

    [Fact]
    public void AddErrorCatalog_RegistersTheAggregateFromEveryRegistration()
    {
        ServiceCollection services = new();

        services.AddErrorCatalog(typeof(FakeModuleErrors));
        services.AddErrorCatalog(typeof(OtherModuleErrors));

        using ServiceProvider provider = services.BuildServiceProvider();
        ErrorCatalog catalog = provider.GetRequiredService<ErrorCatalog>();

        catalog.Entries.Select(entry => entry.Code).Should().Contain(["Fake.FieldEntry", "Other.Entry", "Server.Error"]);
        provider.GetServices<ErrorCatalogRegistration>().Select(registration => registration.CatalogType)
            .Should().BeEquivalentTo([typeof(FakeModuleErrors), typeof(OtherModuleErrors)]);
    }

    [Fact]
    public void AddErrorCatalog_FailsEagerlyForANonCatalog()
    {
        ServiceCollection services = new();

        Action act = () => services.AddErrorCatalog(typeof(NotACatalog));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddErrorCatalog_ResolvedAggregateIsASingleton()
    {
        ServiceCollection services = new();
        services.AddErrorCatalog(typeof(FakeModuleErrors));

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<ErrorCatalog>().Should().BeSameAs(provider.GetRequiredService<ErrorCatalog>());
    }

    private static class FakeModuleErrors
    {
        public static readonly ErrorCatalogEntry FieldEntry = new("Fake.FieldEntry", ErrorKind.NotFound, "Field entry.");

        public static ErrorCatalogEntry PropertyEntry { get; } = new("Fake.PropertyEntry", ErrorKind.Conflict, "Property entry.");

        public const string NotAnEntry = "ignored";

        public static int AlsoNotAnEntry => 42;
    }

    private static class OtherModuleErrors
    {
        public static readonly ErrorCatalogEntry Entry = new("Other.Entry", ErrorKind.Validation, "Other entry.");
    }

    private static class DuplicatingErrors
    {
        public static readonly ErrorCatalogEntry Clash = new("Fake.FieldEntry", ErrorKind.Validation, "Clashes.");
    }

    private static class RedeclaresSharedErrors
    {
        public static readonly ErrorCatalogEntry Clash = new("Http.NotFound", ErrorKind.NotFound, "Clashes.");
    }

    private static class NotACatalog
    {
        public const string Nothing = "nothing";
    }
}
