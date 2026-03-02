using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Announcements.Entities;

public sealed class ChangelogEntry : AggregateRoot<ChangelogEntryId>
{
    public string Version { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public DateTime ReleasedAt { get; private set; }
    public bool IsPublished { get; private set; }

    private readonly List<ChangelogItem> _items = [];
    public IReadOnlyList<ChangelogItem> Items => _items.AsReadOnly();

    private ChangelogEntry() { }

    private ChangelogEntry(string version, string title, string content, DateTime releasedAt, TimeProvider timeProvider)
        : base(ChangelogEntryId.New())
    {
        Version = version;
        Title = title;
        Content = content;
        ReleasedAt = releasedAt;
        IsPublished = false;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static ChangelogEntry Create(string version, string title, string content, DateTime releasedAt, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new ChangelogEntry(version, title, content, releasedAt, timeProvider);
    }

    public void Update(string version, string title, string content, DateTime releasedAt, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Version = version;
        Title = title;
        Content = content;
        ReleasedAt = releasedAt;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Publish(TimeProvider timeProvider)
    {
        IsPublished = true;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public void Unpublish(TimeProvider timeProvider)
    {
        IsPublished = false;
        SetUpdated(timeProvider.GetUtcNow());
    }

    public ChangelogItem AddItem(string description, ChangeType type, TimeProvider timeProvider)
    {
        ChangelogItem item = ChangelogItem.Create(Id, description, type);
        _items.Add(item);
        SetUpdated(timeProvider.GetUtcNow());
        return item;
    }

    public void RemoveItem(ChangelogItemId itemId, TimeProvider timeProvider)
    {
        ChangelogItem? item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is not null)
        {
            _items.Remove(item);
            SetUpdated(timeProvider.GetUtcNow());
        }
    }
}
