using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Wallow.Identity.Api.Contracts.Requests;

namespace Wallow.Identity.Tests.Api.Contracts;

/// <summary>
/// A validation attribute on a positional record must sit on the constructor PARAMETER, not on
/// the generated property. MVC refuses to bind a record whose validation metadata landed on the
/// property — every request to that endpoint returns 500 — and the `[property: ...]` target that
/// causes it compiles cleanly and reads as correct, so nothing else catches it.
///
/// The sweep covers the whole contracts namespace because the mistake is per-record, and a spec
/// naming one record leaves the next one unguarded.
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
        // A namespace typo or a moved contracts assembly would otherwise leave this file
        // asserting nothing while still passing.
        RequestRecords().Count.Should().BeGreaterThan(10);
    }

    /// <summary>
    /// The record's positional constructor: the one whose parameters all have a same-named
    /// property, which is what MVC binds a request body through.
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
