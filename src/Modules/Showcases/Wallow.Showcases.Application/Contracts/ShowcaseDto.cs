using Wallow.Showcases.Domain.Enums;
using Wallow.Showcases.Domain.Identity;

namespace Wallow.Showcases.Application.Contracts;

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
