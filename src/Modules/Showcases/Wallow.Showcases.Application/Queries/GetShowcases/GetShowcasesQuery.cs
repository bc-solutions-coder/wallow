using Wallow.Showcases.Domain.Enums;

namespace Wallow.Showcases.Application.Queries.GetShowcases;

public sealed record GetShowcasesQuery(ShowcaseCategory? Category, string? Tag);
