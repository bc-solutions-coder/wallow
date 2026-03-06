using Foundry.Showcases.Domain.Enums;

namespace Foundry.Showcases.Application.Queries.GetShowcases;

public sealed record GetShowcasesQuery(ShowcaseCategory? Category, string? Tag);
