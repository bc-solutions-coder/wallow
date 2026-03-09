using System.Reflection;
using NetArchTest.Rules;

#pragma warning disable CA1024 // MemberData source methods cannot be properties
#pragma warning disable CA1310 // String comparison in LINQ lambdas over type names is culture-safe

namespace Foundry.Architecture.Tests;

public class WolverineConventionTests
{
    public static IEnumerable<object[]> GetModuleNames()
    {
        foreach (string moduleName in TestConstants.AllModules)
        {
            yield return [moduleName];
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void Handlers_ShouldHave_HandleOrHandleAsyncMethod(string moduleName)
    {
        Assembly applicationAssembly = Assembly.Load($"Foundry.{moduleName}.Application");

        List<Type> handlerTypes = Types.InAssembly(applicationAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsNested)
            .ToList();

        foreach (Type handler in handlerTypes)
        {
            MethodInfo[] methods = handler.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            bool hasHandleMethod = methods.Any(m =>
                m.Name is "Handle" or "HandleAsync");

            hasHandleMethod.Should().BeTrue(
                $"Handler {handler.FullName} should have a public Handle or HandleAsync method " +
                "following Wolverine conventions");
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void DomainEvents_ShouldFollowPastTenseNaming(string moduleName)
    {
        Assembly domainAssembly = Assembly.Load($"Foundry.{moduleName}.Domain");

        List<Type> domainEventTypes = Types.InAssembly(domainAssembly)
            .That()
            .HaveNameEndingWith("DomainEvent")
            .And()
            .AreNotAbstract()
            .GetTypes()
            .ToList();

        string[] pastTenseSuffixes =
        [
            "CreatedDomainEvent", "UpdatedDomainEvent", "DeletedDomainEvent",
            "RemovedDomainEvent", "AddedDomainEvent", "ChangedDomainEvent",
            "IssuedDomainEvent", "PaidDomainEvent", "CancelledDomainEvent",
            "CompletedDomainEvent", "StartedDomainEvent", "StoppedDomainEvent",
            "ArchivedDomainEvent", "PublishedDomainEvent", "SubmittedDomainEvent",
            "ApprovedDomainEvent", "RejectedDomainEvent", "ExpiredDomainEvent",
            "SentDomainEvent", "ReceivedDomainEvent", "ResolvedDomainEvent",
            "ScheduledDomainEvent", "TriggeredDomainEvent", "ProcessedDomainEvent",
            "OverdueDomainEvent", "ActivatedDomainEvent", "DeactivatedDomainEvent",
            "SkippedDomainEvent", "FailedDomainEvent", "BookedDomainEvent",
            "ReleasedDomainEvent", "ReservedDomainEvent", "AdjustedDomainEvent",
            "FulfilledDomainEvent", "PlacedDomainEvent", "RectifiedDomainEvent",
            "ErasedDomainEvent", "RequestedDomainEvent", "RegisteredDomainEvent",
            "LoggedDomainEvent", "MentionedDomainEvent", "PostedDomainEvent",
            "UploadedDomainEvent", "DownloadedDomainEvent", "GeneratedDomainEvent",
            "ConfiguredDomainEvent", "EnabledDomainEvent", "DisabledDomainEvent",
            "ConnectedDomainEvent", "DisconnectedDomainEvent", "SuspendedDomainEvent",
            "RestoredDomainEvent", "MovedDomainEvent", "RenamedDomainEvent",
            "AssignedDomainEvent", "UnassignedDomainEvent", "ClosedDomainEvent",
            "OpenedDomainEvent", "MergedDomainEvent", "SplitDomainEvent",
            "TransferredDomainEvent", "LinkedDomainEvent", "UnlinkedDomainEvent",
            "ReadDomainEvent", "EditedDomainEvent", "HeldDomainEvent",
            "ConfirmedDomainEvent", "AnonymizedDomainEvent"
        ];

        foreach (Type domainEvent in domainEventTypes)
        {
            bool followsPastTense = pastTenseSuffixes.Any(suffix =>
                domainEvent.Name.EndsWith(suffix));

            followsPastTense.Should().BeTrue(
                $"Domain event {domainEvent.FullName} should use past-tense naming " +
                "(e.g., InvoiceCreatedDomainEvent, PaymentCreatedDomainEvent)");
        }
    }

    [Fact]
    public void IntegrationEventContracts_ShouldBeRecords()
    {
        Assembly contractsAssembly = Assembly.Load("Foundry.Shared.Contracts");

        List<Type> integrationEventTypes = Types.InAssembly(contractsAssembly)
            .That()
            .HaveNameEndingWith("Event")
            .And()
            .AreNotAbstract()
            .And()
            .AreNotInterfaces()
            .GetTypes()
            .ToList();

        foreach (Type eventType in integrationEventTypes)
        {
            bool isRecord = eventType.GetMethod("<Clone>$") is not null;

            isRecord.Should().BeTrue(
                $"Integration event {eventType.FullName} in Shared.Contracts should be a record " +
                "for immutability and serialization");
        }
    }
}
