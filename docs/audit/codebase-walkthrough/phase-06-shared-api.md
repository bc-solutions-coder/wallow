# Phase 6: Shared API

**Scope:** `src/Shared/Wallow.Shared.Api/`
**Status:** Not Started
**Files:** 1 source file, 0 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Shared/Wallow.Shared.Api/Extensions/ResultExtensions.cs` | Extension methods converting Result/Result<T> to ASP.NET Core IActionResult using RFC 7807 Problem Details | `ToActionResult`, `ToActionResult<T>`, `ToCreatedResult<T>`, `ToNoContentResult`; maps error codes to HTTP status codes (NotFound->404, Validation->400, Conflict->409, Unauthorized->401, Forbidden->403) | Kernel.Results, Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Mvc | |

## Test Files

No dedicated test project for Shared.Api. The `ResultExtensions` are tested implicitly through API integration tests in `tests/Wallow.Api.Tests/`.
