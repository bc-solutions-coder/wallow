using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.RegisterSetupClient;

public sealed partial class RegisterSetupClientHandler(
    ISetupClientService setupClientService,
    ILogger<RegisterSetupClientHandler> logger)
{
    public async Task<Result<RegisterSetupClientResult>> Handle(
        RegisterSetupClientCommand command,
        CancellationToken ct)
    {
        bool exists = await setupClientService.ClientExistsAsync(command.ClientId, ct);
        if (exists)
        {
            return Result.Failure<RegisterSetupClientResult>(
                Error.Conflict($"Client '{command.ClientId}' already exists"));
        }

        string plaintextSecret = GenerateSecret();

        await setupClientService.CreateConfidentialClientAsync(
            command.ClientId,
            plaintextSecret,
            command.RedirectUris,
            ct);

        LogSetupClientRegistered(command.ClientId);

        return Result.Success(new RegisterSetupClientResult(plaintextSecret));
    }

    private static string GenerateSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered setup client: {ClientId}")]
    private partial void LogSetupClientRegistered(string clientId);
}
