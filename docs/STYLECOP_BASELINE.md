# StyleCop Baseline Configuration

## Overview

StyleCop.Analyzers is configured to enforce C# code style consistency across the Wallow codebase. This document describes the baseline configuration and intentional exceptions.

## Configuration Files

| File | Purpose |
|------|---------|
| `Directory.Packages.props` | Defines StyleCop.Analyzers version (1.2.0-beta.556) |
| `Directory.Build.targets` | Applies StyleCop to all non-test projects |
| `stylecop.json` | StyleCop-specific settings (documentation, naming, ordering) |
| `.editorconfig` | Rule severity overrides and exceptions |

**Note:** Version 1.2.0-beta.556 is used as it's the latest available pre-release with .NET 10 support. The stable 1.1.x line does not support C# 13 features.

## Enabled Rule Categories

### Spacing Rules (SA1000-SA1028)
- Enforces consistent spacing around operators, braces, parentheses
- Severity: **warning**
- Aligns with existing .editorconfig spacing preferences

### Ordering Rules
- **SA1200**: Using directives outside namespace - **warning** (enforced)
- **SA1208**: System using directives first - **warning** (enforced)
- Matches existing codebase convention

### Naming Rules
- **SA1300**: Elements begin with upper-case - **warning**
- **SA1303**: Const fields upper-case - **warning**
- **SA1311**: Static readonly fields upper-case - **warning**
- **SA1309/SA1310**: Field underscore rules - **none** (allows `_privateField` convention)

### Maintainability Rules
- **SA1400**: Access modifiers required - **warning**
- **SA1401**: Fields should be private - **warning**

### Layout Rules
- **SA1500**: Braces on separate lines - **warning**

## Disabled Rules (Baseline Exceptions)

### Documentation Rules - Deferred to Phase 0 Cleanup

All documentation rules are disabled during the baseline period. These will be enabled incrementally as part of the Foundation audit.

| Rule | Reason |
|------|--------|
| SA0001 | XML comment analysis disabled - re-enable after documentation audit |
| SA1600 | Elements should be documented - 7,130 existing violations |
| SA1601 | Partial elements should be documented |
| SA1602 | Enumeration items should be documented |
| SA1633 | File headers - not required (no copyright headers in source) |
| SA1652 | Enable XML documentation output |

### Readability Rules - Deferred

| Rule | Reason |
|------|--------|
| SA1101 | Prefix with `this` - not our style (explicitly avoided per .editorconfig) |
| SA1413 | Trailing commas - not enforced, optional |

### Layout Rules - Deferred

| Rule | Reason |
|------|--------|
| SA1516 | Element separation - too noisy, will address in cleanup |

### Naming Rules - Intentional Exceptions

| Rule | Reason |
|------|--------|
| SA1306 | Field lower-case - conflicts with private field `_` prefix |
| SA1309 | No underscore prefix - **conflicts with codebase standard** |
| SA1310 | No underscores in fields - conflicts with test naming |

## Integration with .NET Analyzers

StyleCop complements the existing Microsoft.CodeAnalysis.NetAnalyzers. Current baseline warnings:

| Analyzer | Count | Status |
|----------|-------|--------|
| CA1062 | 1,718 | Null validation - Phase 0 task |
| IDE0011 | 1,418 | Braces required - Phase 0 task |
| IDE1006 | 1,366 | Naming conventions - Phase 0 task |
| CA1848 | 936 | Structured logging - Phase 0 task |
| **Total .NET Analyzers** | **~7,130** | Phase 0 cleanup |
| **StyleCop (after baseline)** | **1,072** | Reduced from 7,822 |

### Remaining StyleCop Warnings (Post-Baseline)

| Rule | Count | Description | Phase |
|------|-------|-------------|-------|
| SA1009 | 958 | Closing parenthesis spacing | Phase 0 |
| SA1028 | 58 | Trailing whitespace | Phase 0 |
| SA1137 | 22 | Element indentation | Phase 0 |
| SA1208 | 14 | System using directives first | Phase 0 |
| SA1414 | 8 | Tuple element names | Phase 0 |
| SA1316 | 4 | Tuple element names should use correct casing | Phase 0 |
| SA1200 | 4 | Using directives placement | Phase 0 |
| SA1107 | 2 | Multiple statements on one line | Phase 0 |
| SA1025 | 2 | Code should not contain multiple spaces | Phase 0 |

## Rollout Plan

### Phase 0 (Current)
- StyleCop.Analyzers installed
- Baseline rules configured
- Conflicting rules disabled
- Documentation rules deferred

### Phase 0 Cleanup Tasks
1. Fix IDE0011 (add braces) - 1,418 warnings
2. Fix CA1062 (null validation) - 1,718 warnings
3. Fix IDE1006 (naming) - 1,366 warnings
4. Document public APIs (enable SA1600 incrementally)

### Future Phases
- Enable documentation rules per module during audits
- Enable SA1516 (element separation) during code cleanup
- Re-evaluate SA1413 (trailing commas) for consistency

## Usage

### Building with StyleCop
```bash
dotnet build  # StyleCop runs automatically
```

### Viewing StyleCop Violations
```bash
dotnet build 2>&1 | grep "warning SA"
```

### Suppressing a Rule in Code
```csharp
#pragma warning disable SA1600 // Missing documentation
public class Example { }
#pragma warning restore SA1600
```

### Disabling a Rule Globally
Add to `.editorconfig`:
```ini
dotnet_diagnostic.SA1234.severity = none
```

## References

- [StyleCop.Analyzers Documentation](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)
- [Rule Index](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/DOCUMENTATION.md)
- [Wallow .editorconfig](/Users/traveler/Repos/Wallow/.editorconfig)
- [Codebase Perfection Audit Design](/Users/traveler/Repos/Wallow/docs/plans/2026-02-16-codebase-perfection-audit-design.md)
