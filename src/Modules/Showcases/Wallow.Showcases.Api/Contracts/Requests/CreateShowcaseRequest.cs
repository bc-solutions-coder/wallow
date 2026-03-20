using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Api.Contracts.Requests;

public sealed record CreateShowcaseRequest(
    string Title,
    string? Description,
    ShowcaseCategory Category,
    string? DemoUrl,
    string? GitHubUrl,
    string? VideoUrl,
    IReadOnlyList<string>? Tags,
    int DisplayOrder,
    bool IsPublished);
