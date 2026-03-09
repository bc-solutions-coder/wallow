using System.Collections.Frozen;
using System.Text;
using FluentValidation;
using Foundry.Shared.Contracts.Storage.Commands;

namespace Foundry.Storage.Application.Commands.UploadFile;

public sealed class UploadFileValidator : AbstractValidator<UploadFileCommand>
{
    private static readonly FrozenDictionary<string, byte[][]> _magicBytesByContentType = new Dictionary<string, byte[][]>
    {
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47]],
        ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46]],
        ["image/gif"] = [[0x47, 0x49, 0x46, 0x38]],
        ["application/zip"] = [[0x50, 0x4B, 0x03, 0x04]],
    }.ToFrozenDictionary();

    // PE executables and DLLs share the MZ header
    private static readonly byte[] _mzHeader = [0x4D, 0x5A];

    // Text-based dangerous signatures checked case-insensitively
    private static readonly string[] _blockedTextSignatures = ["<html", "<!doctype", "<svg"];

    // Max bytes to read for blocked signature detection
    private const int BlockedSignatureMaxBytes = 16;

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

        RuleFor(x => x.FileName)
            .Must(name => !ContainsPathTraversal(name))
            .WithMessage("File name must not contain path traversal sequences")
            .When(x => !string.IsNullOrEmpty(x.FileName));

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required")
            .MaximumLength(200).WithMessage("Content type must not exceed 200 characters");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0).WithMessage("File size must be greater than 0");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("Content is required");

        RuleFor(x => x.Path)
            .MaximumLength(500).WithMessage("Path must not exceed 500 characters")
            .Must(path => !ContainsPathTraversal(path!))
            .WithMessage("Path must not contain path traversal sequences")
            .When(x => x.Path is not null);

        RuleFor(x => x)
            .MustAsync(HaveMatchingMagicBytesAsync)
            .WithMessage("File content does not match the declared content type")
            .When(x => x.Content is not null && _magicBytesByContentType.ContainsKey(x.ContentType));

        RuleFor(x => x)
            .MustAsync(NotContainBlockedSignatureAsync)
            .WithMessage("File contains a blocked file type signature (HTML, SVG, or executable)")
            .When(x => x.Content is not null);
    }

    private static bool ContainsPathTraversal(string value)
    {
        return value.Contains("..", StringComparison.Ordinal) ||
               value.StartsWith('/') ||
               value.StartsWith('\\') ||
               (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');
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

    private static async Task<bool> NotContainBlockedSignatureAsync(
        UploadFileCommand command, CancellationToken cancellationToken)
    {
        byte[] header = new byte[BlockedSignatureMaxBytes];

        long originalPosition = command.Content.CanSeek ? command.Content.Position : 0;
        int bytesRead = await command.Content.ReadAsync(header.AsMemory(0, BlockedSignatureMaxBytes), cancellationToken);

        if (command.Content.CanSeek)
        {
            command.Content.Position = originalPosition;
        }

        if (bytesRead < 2)
        {
            return true;
        }

        // Check for PE/DLL (MZ header)
        if (header.AsSpan(0, _mzHeader.Length).SequenceEqual(_mzHeader))
        {
            return false;
        }

        // Check for text-based dangerous signatures (case-insensitive)
        string headerText = Encoding.ASCII.GetString(header, 0, bytesRead).ToLowerInvariant();

        string trimmedHeader = headerText.TrimStart();

        foreach (string blocked in _blockedTextSignatures)
        {
            if (trimmedHeader.StartsWith(blocked, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
