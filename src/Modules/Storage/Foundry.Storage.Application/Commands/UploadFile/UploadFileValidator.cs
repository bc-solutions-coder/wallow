using FluentValidation;
using Foundry.Shared.Contracts.Storage.Commands;

namespace Foundry.Storage.Application.Commands.UploadFile;

public sealed class UploadFileValidator : AbstractValidator<UploadFileCommand>
{
    private static readonly Dictionary<string, byte[][]> _magicBytesByContentType = new()
    {
        ["image/jpeg"] = [new byte[] { 0xFF, 0xD8, 0xFF }],
        ["image/png"] = [new byte[] { 0x89, 0x50, 0x4E, 0x47 }],
        ["application/pdf"] = [new byte[] { 0x25, 0x50, 0x44, 0x46 }],
        ["image/gif"] = [new byte[] { 0x47, 0x49, 0x46, 0x38 }],
        ["application/zip"] = [new byte[] { 0x50, 0x4B, 0x03, 0x04 }],
    };

    public UploadFileValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.BucketName)
            .NotEmpty().WithMessage("Bucket name is required")
            .MaximumLength(100).WithMessage("Bucket name must not exceed 100 characters");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required")
            .MaximumLength(500).WithMessage("File name must not exceed 500 characters");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required")
            .MaximumLength(200).WithMessage("Content type must not exceed 200 characters");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than 0");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("Content is required");

        RuleFor(x => x.Path)
            .MaximumLength(500).WithMessage("Path must not exceed 500 characters")
            .When(x => x.Path is not null);

        RuleFor(x => x)
            .MustAsync(HaveMatchingMagicBytesAsync)
            .WithMessage("File content does not match the declared content type")
            .When(x => x.Content is not null && _magicBytesByContentType.ContainsKey(x.ContentType));
    }

    private static async Task<bool> HaveMatchingMagicBytesAsync(
        UploadFileCommand command, CancellationToken cancellationToken)
    {
        byte[][] signatures = _magicBytesByContentType[command.ContentType];

        int maxLength = signatures.Max(s => s.Length);
        byte[] header = new byte[maxLength];

        long originalPosition = command.Content.CanSeek ? command.Content.Position : 0;
        int bytesRead = await command.Content.ReadAsync(header.AsMemory(0, maxLength), cancellationToken);

        if (command.Content.CanSeek)
        {
            command.Content.Position = originalPosition;
        }

        if (bytesRead < signatures.Min(s => s.Length))
        {
            return false;
        }

        return signatures.Any(sig =>
            bytesRead >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
    }
}
