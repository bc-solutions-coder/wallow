using Foundry.Showcases.Domain.Enums;

namespace Foundry.Showcases.Api.Contracts.Requests;

public sealed record UpdateShowcaseRequest(
    string Title,
    string? Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    IReadOnlyList<string>? Tags,
    int DisplayOrder,
    bool IsPublished);
