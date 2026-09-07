using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Inquiries.Application.Commands.SubmitInquiry;
using Wallow.Inquiries.Application.DTOs;
using Wallow.Shared.Kernel.Results;
using Wallow.Tests.Common.Factories;
using Wolverine;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Checks single validator registration for selected messages and invokes validation through Wolverine.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ValidatorRegistrationTests(WallowApiFactory factory)
{
    [Fact]
    public void Each_Validated_Message_Type_Has_Exactly_One_Validator_Registration()
    {
        using IServiceScope scope = factory.Services.CreateScope();

        List<string> offenders = [];

        foreach (Type messageType in DiscoverValidatedMessageTypes())
        {
            Type serviceType = typeof(IValidator<>).MakeGenericType(messageType);
            int registrations = scope.ServiceProvider.GetServices(serviceType).Count();

            if (registrations != 1)
            {
                offenders.Add($"{messageType.FullName} has {registrations} IValidator registrations");
            }
        }

        offenders.Should().BeEmpty(
            "every validated message must resolve a single validator; found {0}",
            string.Join("; ", offenders));
    }

    [Fact]
    public async Task Validated_Command_Runs_Its_Validator_Through_The_Wolverine_Pipeline()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        SubmitInquiryCommand invalidCommand = new(
            Name: string.Empty,
            Email: "not-an-email",
            Phone: string.Empty,
            Company: null,
            SubmitterId: null,
            ProjectType: string.Empty,
            BudgetRange: string.Empty,
            Timeline: string.Empty,
            Message: string.Empty);

        Func<Task> act = () => bus.InvokeAsync<Result<InquiryDto>>(invalidCommand);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private static IEnumerable<Type> DiscoverValidatedMessageTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true)
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
            .Select(GetValidatedMessageType)
            .Where(messageType => messageType is not null)
            .Select(messageType => messageType!)
            .Distinct();
    }

    private static Type? GetValidatedMessageType(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Select(type => type!);
        }
    }
}
