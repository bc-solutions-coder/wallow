using Microsoft.Extensions.Logging;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

public sealed partial class BootstrapAdminHandler(
    IBootstrapAdminService bootstrapAdminService,
    ILogger<BootstrapAdminHandler> logger)
{
    private const string AdminRoleName = "admin";

    public async Task<Result> Handle(BootstrapAdminCommand command, CancellationToken ct)
    {
        bool userExists = await bootstrapAdminService.UserExistsAsync(command.Email, ct);
        if (userExists)
        {
            LogAdminAlreadyExists(command.Email);
            return Result.Success();
        }

        await bootstrapAdminService.EnsureRoleExistsAsync(AdminRoleName, ct);

        Guid userId = await bootstrapAdminService.CreateUserAsync(
            command.Email,
            command.Password,
            command.FirstName,
            command.LastName,
            ct);

        await bootstrapAdminService.AssignRoleAsync(userId, AdminRoleName, ct);

        LogAdminBootstrapped(command.Email, userId);

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin user already exists for email {Email}, skipping bootstrap")]
    private partial void LogAdminAlreadyExists(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrapped admin user {Email} with ID {UserId}")]
    private partial void LogAdminBootstrapped(string email, Guid userId);
}
