using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Showcases.Domain.Entities;

public sealed class Showcase : AggregateRoot<ShowcaseId>
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ShowcaseCategory Category { get; private set; }
    public string? DemoUrl { get; private set; }
    public string? GitHubUrl { get; private set; }
    public string? VideoUrl { get; private set; }

    private readonly List<string> _tags = [];
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    public int DisplayOrder { get; private set; }
    public bool IsPublished { get; private set; }

    private Showcase() { } // EF Core

    public static Result<Showcase> Create(
        string title,
        string? description,
        ShowcaseCategory category,
        string? demoUrl,
        string? gitHubUrl,
        string? videoUrl,
        IReadOnlyList<string>? tags = null,
        int displayOrder = 0,
        bool isPublished = false)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<Showcase>(Error.Validation("Showcase.TitleRequired", "Title is required"));
        }

        if (title.Length > 200)
        {
            return Result.Failure<Showcase>(Error.Validation("Showcase.TitleTooLong", "Title must be 200 characters or fewer"));
        }

        if (string.IsNullOrWhiteSpace(demoUrl) && string.IsNullOrWhiteSpace(gitHubUrl) && string.IsNullOrWhiteSpace(videoUrl))
        {
            return Result.Failure<Showcase>(Error.Validation("Showcase.UrlRequired", "At least one of DemoUrl, GitHubUrl, or VideoUrl must be provided"));
        }

        Showcase showcase = new()
        {
            Id = ShowcaseId.New(),
            Title = title.Trim(),
            Description = description?.Trim(),
            Category = category,
            DemoUrl = demoUrl?.Trim(),
            GitHubUrl = gitHubUrl?.Trim(),
            VideoUrl = videoUrl?.Trim(),
            DisplayOrder = displayOrder,
            IsPublished = isPublished
        };

        if (tags is { Count: > 0 })
        {
            showcase._tags.AddRange(tags);
        }

        return Result.Success(showcase);
    }

    public Result Update(
        string title,
        string? description,
        ShowcaseCategory category,
        string? demoUrl,
        string? gitHubUrl,
        string? videoUrl,
        IReadOnlyList<string>? tags = null,
        int displayOrder = 0,
        bool isPublished = false)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("Showcase.TitleRequired", "Title is required"));
        }

        if (title.Length > 200)
        {
            return Result.Failure(Error.Validation("Showcase.TitleTooLong", "Title must be 200 characters or fewer"));
        }

        if (string.IsNullOrWhiteSpace(demoUrl) && string.IsNullOrWhiteSpace(gitHubUrl) && string.IsNullOrWhiteSpace(videoUrl))
        {
            return Result.Failure(Error.Validation("Showcase.UrlRequired", "At least one of DemoUrl, GitHubUrl, or VideoUrl must be provided"));
        }

        Title = title.Trim();
        Description = description?.Trim();
        Category = category;
        DemoUrl = demoUrl?.Trim();
        GitHubUrl = gitHubUrl?.Trim();
        VideoUrl = videoUrl?.Trim();
        DisplayOrder = displayOrder;
        IsPublished = isPublished;

        _tags.Clear();
        if (tags is { Count: > 0 })
        {
            _tags.AddRange(tags);
        }

        return Result.Success();
    }
}
