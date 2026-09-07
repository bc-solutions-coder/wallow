using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Wallow.Identity.Api.Contracts.Requests;

namespace Wallow.Identity.Tests.Api.Contracts;

/// <summary>
/// Checks that positional request-record validation attributes target constructor parameters.
/// </summary>
public class RequestRecordValidationMetadataTests
{
    public static TheoryData<Type> RequestRecords()
    {
        TheoryData<Type> data = new();
        foreach (Type type in typeof(UpdateOrganizationEnrollmentRequest).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true }
                && t.Namespace == typeof(UpdateOrganizationEnrollmentRequest).Namespace
                && PrimaryConstructorOf(t) is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            data.Add(type);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RequestRecords))]
    public void APositionalRecord_CarriesNoValidationOnItsGeneratedProperties(Type requestType)
    {
        ConstructorInfo primary = PrimaryConstructorOf(requestType)!;

        List<string> misplaced = [];
        foreach (ParameterInfo parameter in primary.GetParameters())
        {
            PropertyInfo? generated = requestType.GetProperty(
                parameter.Name!, BindingFlags.Public | BindingFlags.Instance);

            if (generated is not null
                && generated.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
            {
                misplaced.Add(parameter.Name!);
            }
        }

        misplaced.Should().BeEmpty(
            "MVC ignores validation metadata on a positional record's properties and throws while "
            + "binding; drop the `property:` target so the attribute lands on the parameter");
    }

    [Fact]
    public void TheSweep_ActuallyFindsTheRequestRecords()
    {
        // Require enough discovered records to catch an empty or misdirected scan.
        RequestRecords().Count.Should().BeGreaterThan(10);
    }

    /// <summary>
    /// Finds the first public nonempty constructor whose parameters have matching public properties.
    /// </summary>
    private static ConstructorInfo? PrimaryConstructorOf(Type type)
    {
        foreach (ConstructorInfo candidate in type.GetConstructors())
        {
            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length > 0
                && parameters.All(p => type.GetProperty(
                    p.Name!, BindingFlags.Public | BindingFlags.Instance) is not null))
            {
                return candidate;
            }
        }

        return null;
    }
}
