# Package Bookmarks

Packages and tools worth evaluating for future use. Not yet adopted - bookmarked for reference.

## Feature Flag Management

### FeatBit

- **URL:** https://www.featbit.co/
- **GitHub:** https://github.com/featbit/featbit
- **What:** Open-source feature flag platform built in .NET. Self-hostable, no seat limits, admin UI included.
- **Why:** Integrates with `Microsoft.FeatureManagement` as a custom provider - swap in without changing application code. Adds admin UI, per-environment flag states, and tenant targeting.
- **When to adopt:** When managing flags via `appsettings.json` becomes painful across multiple environments, or when non-developers need to toggle dev gates.
- **Bookmarked:** 2026-03-10

## Feature Flag Standards

### OpenFeature

- **URL:** https://openfeature.dev/
- **GitHub:** https://github.com/open-feature/dotnet-sdk
- **What:** Vendor-agnostic open specification for feature flag evaluation. Provides a standard API that works with any backing provider (LaunchDarkly, Flagsmith, FeatBit, Microsoft.FeatureManagement).
- **Why:** Prevents vendor lock-in if we ever adopt an external feature flag service. Has an existing provider for `Microsoft.FeatureManagement`.
- **When to adopt:** When switching between or combining multiple feature flag providers becomes a real need.
- **Bookmarked:** 2026-03-10
