using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;

namespace Foundry.Showcases.Application.Contracts;

public record ShowcaseDto(
    ShowcaseId Id,
    string Title,
    string? Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    IReadOnlyList<string> Tags,
    int DisplayOrder,
    bool IsPublished);
