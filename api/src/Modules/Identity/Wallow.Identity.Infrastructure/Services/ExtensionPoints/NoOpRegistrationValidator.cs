using System.Diagnostics.CodeAnalysis;

using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

[ExcludeFromCodeCoverage]
internal sealed class NoOpRegistrationValidator : IRegistrationValidator
{
    public Task<Result> ValidateAsync(string email, string? displayName, CancellationToken ct)
    {
        return Task.FromResult(Result.Success());
    }
}
