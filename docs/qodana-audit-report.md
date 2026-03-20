# Qodana Code Quality Audit Report

**Date:** 2026-03-02
**Tool:** Qodana Community for .NET 2025.3
**Branch:** `expansion`
**Total Issues:** 2,830 (12 errors, 736 warnings, 2,082 notes)

---

## Executive Summary

The Wallow codebase has 2,830 code quality findings across all modules. The vast majority (73%) are stylistic/note-level suggestions, with 26% being warnings and less than 1% being errors. No critical security vulnerabilities were detected.

The top 5 issue categories account for **1,802 findings (64%)** and are largely mechanical fixes suitable for automated remediation:

| Category | Count | % of Total | Auto-fixable? |
|----------|-------|-----------|---------------|
| `new()` target-typed expressions | 1,004 | 35.5% | Yes |
| Collection expression syntax | 304 | 10.7% | Yes |
| Unused members (non-private) | 173 | 6.1% | Needs review |
| Primary constructors | 169 | 6.0% | Yes |
| Redundant null suppression (`!`) | 152 | 5.4% | Yes |

---

## Module Breakdown

| Module | Issues | % of Total |
|--------|--------|-----------|
| Identity | 1,067 | 37.7% |
| Billing | 480 | 17.0% |
| Tests (cross-module) | 366 | 12.9% |
| Configuration | 236 | 8.3% |
| Communications | 227 | 8.0% |
| Shared | 187 | 6.6% |
| Storage | 164 | 5.8% |
| Api | 103 | 3.6% |

Identity is the largest module and has the most issues proportionally.

---

## Phase 1: Errors (Must Fix) — 12 issues

These are the only actual errors and should be fixed immediately.

### 1.1 Unnecessary Using Directives (IDE0005) — 12 errors

Unused `using` statements that the compiler flags as errors. Simple removal.

**Effort:** ~15 minutes
**Risk:** None

---

## Phase 2: High-Value Warnings (Bug Risk) — ~75 issues

These warnings indicate potential bugs, runtime errors, or correctness issues.

### 2.1 Possible Multiple Enumeration — 25 warnings

Enumerables that are iterated more than once, which can cause bugs with database queries or deferred execution.

**Files:** `CustomFieldDefinition.cs`, `KeycloakAdminService.cs`, Identity module handlers
**Fix:** Materialize with `.ToList()` or restructure to single-pass
**Effort:** 2-3 hours
**Risk:** Medium (behavior change possible)

### 2.2 Access to Disposed Closure — 1 warning

Captured variable used after disposal in `TenantSaveChangesInterceptorTests.cs`.

**Fix:** Restructure test to avoid accessing disposed context
**Effort:** 15 minutes
**Risk:** Low (test code only)

### 2.3 String Formatting Problems — 3 warnings

Format string arguments not matching placeholders in `CustomFieldIndexManager.cs`.

**Fix:** Correct format strings or remove unused arguments
**Effort:** 15 minutes
**Risk:** Low (could fix logging output)

### 2.4 Empty Catch Clauses — 3 warnings

Swallowing all exceptions silently in `PluginLoaderTests.cs` and `CustomFieldIndexManagerTests.cs`.

**Fix:** Log or assert on expected exceptions
**Effort:** 30 minutes
**Risk:** Low

### 2.5 Expression Always True/False (Nullable) — 3 warnings

Null checks on values that nullable annotations say can never be null.

**Fix:** Remove redundant checks or fix annotations
**Effort:** 15 minutes
**Risk:** Low

### 2.6 Null-Coalescing on Non-Nullable — 3 warnings

`??` operators on values that can never be null per annotations.

**Fix:** Remove redundant null-coalescing
**Effort:** 15 minutes
**Risk:** Low

### 2.7 Get-Only Auto-Property Never Assigned — 1 warning

Property declared but never set.

**Fix:** Remove or assign properly
**Effort:** 10 minutes
**Risk:** Low

### 2.8 Similar Expressions Comparison — 1 warning

Comparing an expression to itself (likely copy-paste bug).

**Fix:** Review and fix comparison
**Effort:** 10 minutes
**Risk:** Low

### 2.9 Cannot Resolve Symbol in Text — 1 warning

Unresolvable symbol reference in a text argument (likely documentation or logging).

**Fix:** Correct the symbol reference
**Effort:** 10 minutes
**Risk:** Low

---

## Phase 3: Code Hygiene Warnings — ~660 issues

These are valid warnings about code quality but lower risk.

### 3.1 Redundant Nullable Warning Suppression (`!`) — 152 warnings

The `!` (null-forgiving operator) is used where the value is already known to be non-null. These are safe to remove and improve code clarity.

**Modules:** Identity (40), Configuration (24), Billing (22), Storage (19), Tests (31), Communications (10), Api (6)
**Fix:** Remove `!` operator
**Effort:** 2-3 hours (mechanical)
**Risk:** None (compiler already knows value is non-null)

### 3.2 Namespace Mismatch — 104 warnings

File namespaces don't match folder structure. Mostly in test projects.

**Modules:** Identity (56), Billing (33), Tests (13), Communications (2)
**Fix:** Update namespaces to match folder paths
**Effort:** 3-4 hours (may require updating test references)
**Risk:** Low (could break test discovery if not careful)

### 3.3 Non-Accessed Positional Properties — 59 warnings

Record positional properties that are never read. Common in DTOs/responses.

**Modules:** Communications (32), Api (5), Shared (5), others
**Fix:** Review if properties are needed for serialization; remove truly unused ones
**Effort:** 2 hours (needs careful review)
**Risk:** Medium (some may be needed for JSON serialization)

### 3.4 Redundant Default Arguments — 58 warnings

Passing explicit values that match the parameter default.

**Modules:** Identity (37), Communications (13), others
**Fix:** Remove redundant arguments
**Effort:** 1-2 hours (mechanical)
**Risk:** None

### 3.5 Unused Auto-Property Accessors — 57 warnings

Property setters or getters that are never called (49 non-private, 8 private).

**Fix:** Convert to get-only or init-only properties
**Effort:** 2 hours
**Risk:** Low (may affect serialization)

### 3.6 Unused Private Members — 45 warnings

Private methods, properties, or fields that are never referenced.

**Fix:** Remove dead code
**Effort:** 1-2 hours
**Risk:** None (private = no external consumers)

### 3.7 Private Fields to Local Variables — 40 warnings

Class fields that are only used in a single method and should be local variables.

**Modules:** Billing (20), others
**Fix:** Convert to local variables
**Effort:** 1-2 hours
**Risk:** None

### 3.8 Redundant Name Qualifiers — 34 warnings

Fully qualified names where a simple name suffices.

**Fix:** Remove namespace qualifiers
**Effort:** 1 hour (mechanical)
**Risk:** None

### 3.9 Variable Can Be Non-Nullable — 19 warnings

Variables declared as nullable but never assigned null.

**Fix:** Remove `?` from type declaration
**Effort:** 30 minutes
**Risk:** None

### 3.10 Conditional Access on Non-Nullable — 16 warnings

`?.` used on expressions that are never null.

**Fix:** Replace `?.` with `.`
**Effort:** 30 minutes
**Risk:** None

### 3.11 Inconsistent Naming — 15 warnings

Constants/enums not following PascalCase conventions (e.g., `AP_SOUTHEAST`, `ATTR`, `BOOL`).

**Files:** `RegionConfiguration.cs`, `ScimToken.cs`, Identity module
**Fix:** Rename to PascalCase
**Effort:** 1-2 hours (need to update all references)
**Risk:** Medium (may affect serialized values or external APIs)

### 3.12 GC.SuppressFinalize Without Destructor — 14 warnings

Calling `GC.SuppressFinalize` on types without finalizers. Common in test `IDisposable` implementations.

**Fix:** Remove the call
**Effort:** 30 minutes
**Risk:** None

### 3.13 Remaining Minor Warnings — ~35 issues

- Collection never updated (12)
- Unused method return values (10)
- Redundant anonymous type property names (10)
- Unused parameters (7)
- Explicit caller info arguments (7)
- Redundant casts (6)
- Redundant type arguments (6)
- Using statement resource initialization (6)
- Variable-length hex escape sequences (6)
- Unused local variables (4)
- Redundant using directives (3)
- Redundant switch arms (2)
- Return type can be non-nullable (2)
- Redundant lambda parameter types (1)

**Fix:** Various mechanical fixes
**Effort:** 2-3 hours total
**Risk:** Low

---

## Phase 4: Modernization (Notes) — ~1,500 issues

These are style suggestions to modernize the codebase to latest C# idioms.

### 4.1 Target-Typed `new()` Expressions — 1,004 notes

Replace `new SomeType()` with `new()` when type is evident from context.

**Modules:** Identity (491), Billing (182), Configuration (98), Storage (46), Api (45), Tests (111), Shared (19), Communications (12)
**Fix:** `SomeType x = new SomeType()` → `SomeType x = new()`
**Effort:** Can be auto-fixed by IDE/Roslyn fixer
**Risk:** None (purely syntactic)

### 4.2 Collection Expression Syntax — 304 notes

Use C# 12 collection expressions (`[item1, item2]`) instead of `new List<T> { ... }`.

**Modules:** Identity (151), Billing (55), Storage (33), Configuration (31), Communications (19), Tests (14)
**Fix:** IDE auto-fix available
**Effort:** Can be auto-fixed
**Risk:** None

### 4.3 Primary Constructors — 169 notes

Convert traditional constructors to C# 12 primary constructors where applicable.

**Modules:** Identity (51), Billing (31), Communications (27), Configuration (18), Tests (16), Shared (13), Api (7), Storage (6)
**Fix:** Convert `class Foo { private readonly IBar _bar; public Foo(IBar bar) { _bar = bar; } }` to `class Foo(IBar bar)`
**Effort:** Semi-automated; needs review for field usage patterns
**Risk:** Low (syntactic change, but fields become captured parameters)

### 4.4 Init-Only Properties — 63 notes

Properties that could use the `init` accessor (39 non-private, 24 private).

**Fix:** Change `set` to `init`
**Effort:** 2 hours
**Risk:** Low (may affect deserialization in some cases)

### 4.5 Object/Collection Initializers — 48 notes

Replace sequential property assignments with initializer syntax.

**Fix:** IDE auto-fix available
**Effort:** 1 hour
**Risk:** None

---

## Phase 5: Dead Code Cleanup — ~280 issues

### 5.1 Unused Non-Private Members — 173 notes

Public/internal types or members that appear unused within the solution.

**Caution:** Some may be used via reflection, serialization, Wolverine handlers, or external consumers. Each needs manual review.

**Fix:** Verify usage and remove confirmed dead code
**Effort:** 4-6 hours (careful review required)
**Risk:** High (false positives common for framework-discovered types)

### 5.2 Unused Types — 30 notes

Entire types that appear unused.

**Fix:** Verify and remove
**Effort:** 1-2 hours
**Risk:** High (same caveats as above)

### 5.3 Unused Parameters — 30 notes

Non-private method parameters that are never read.

**Fix:** Remove or use `_` discard pattern
**Effort:** 1-2 hours
**Risk:** Medium (may be part of interface contracts)

### 5.4 Members Never Accessed Via Base Type — 49 notes

Members declared on base types but only accessed on derived types.

**Fix:** Consider moving to derived type or marking as new
**Effort:** 2 hours
**Risk:** Medium

### 5.5 Miscellaneous — ~30 notes

- Merge pattern checks (22)
- `nameof` for enum members (29)
- Redundant verbatim prefix (13)
- Redundant string interpolation (11)
- Constructor to member initializers (9)
- Redundant params array creation (8)
- Various other small improvements

---

## Phase 6: Advanced Improvements — ~25 issues

### 6.1 `await using` — 6 notes

Convert `using` to `await using` for async disposables.

### 6.2 `System.Threading.Lock` — 2 notes

Use the new `Lock` type instead of `object` for locks.

### 6.3 Method Has Async Overload — 1 note

A synchronous method is called where an async overload exists.

### 6.4 Covariant/Contravariant Type Parameters — 1 note

A generic type parameter could use `in`/`out` variance.

### 6.5 Convert Delegate to Local Function — 2 notes

Lambda stored in variable could be a local function.

---

## Recommended Execution Plan

| Phase | Description | Issues | Effort | Risk | Priority |
|-------|------------|--------|--------|------|----------|
| **1** | Fix errors (unused usings) | 12 | 15 min | None | Immediate |
| **2** | Bug-risk warnings | ~75 | 4-5 hrs | Med | High |
| **3** | Code hygiene warnings | ~660 | 15-20 hrs | Low | Medium |
| **4** | Modernization (auto-fixable) | ~1,500 | 4-6 hrs* | None | Medium |
| **5** | Dead code cleanup | ~280 | 10-12 hrs | High | Low |
| **6** | Advanced improvements | ~25 | 2-3 hrs | Low | Low |

*Phase 4 effort is low because most fixes are IDE-automated.

### Automation Opportunities

1. **Phases 1 & 4** can be almost entirely automated with `dotnet format` and Roslyn code fixers
2. **Phase 3** (sections 3.1, 3.4, 3.8, 3.10, 3.12) are mechanical and can be batched
3. **Phase 5** requires manual review — no automation recommended

### Files With Most Issues (Top 10)

| File | Issues |
|------|--------|
| `PermissionExpansionMiddlewareTests.cs` | 58 |
| `ScimAuthenticationMiddlewareTests.cs` | 41 |
| `ScimToKeycloakTranslatorAdditionalTests.cs` | 36 |
| `ScimUserServiceTests.cs` | 36 |
| `ScimDtos.cs` | 33 |
| `ScimServiceTests.cs` | 32 |
| `BillingValidatorTests.cs` | 31 |
| `ScimSyncHandlerTests.cs` | 31 |
| `ServiceCollectionExtensionsTests.cs` | 24 |
| `FlushUsageJobTests.cs` | 24 |

---

## Appendix: Full Rule Reference

### Errors (12)
| Rule | Count | Description |
|------|-------|-------------|
| IDE0005 | 12 | Unnecessary using directive |

### Warnings (736)
| Rule | Count | Description |
|------|-------|-------------|
| RedundantSuppressNullableWarningExpression | 152 | Redundant `!` operator |
| CheckNamespace | 104 | Namespace doesn't match folder |
| NotAccessedPositionalProperty.Global | 58 | Unused record positional property |
| RedundantArgumentDefaultValue | 58 | Passing default value explicitly |
| UnusedAutoPropertyAccessor.Global | 49 | Unused property accessor |
| UnusedMember.Local | 45 | Unused private member |
| PrivateFieldCanBeConvertedToLocalVariable | 40 | Field should be local variable |
| RedundantNameQualifier | 34 | Unnecessary namespace qualification |
| PossibleMultipleEnumeration | 25 | IEnumerable iterated multiple times |
| VariableCanBeNotNullable | 19 | Variable doesn't need `?` |
| ConditionalAccessQualifierIsNonNullable | 16 | `?.` on non-nullable |
| InconsistentNaming | 15 | Naming convention violation |
| GCSuppressFinalizeForTypeWithoutDestructor | 14 | Unnecessary GC.SuppressFinalize |
| CollectionNeverUpdated.Local | 11 | Collection created but never populated |
| UnusedMethodReturnValue.Local | 10 | Return value ignored |
| RedundantAnonymousTypePropertyName | 10 | Redundant anonymous type name |
| UnusedAutoPropertyAccessor.Local | 8 | Unused private property accessor |
| ExplicitCallerInfoArgument | 7 | Explicit CallerInfo argument |
| UnusedParameter.Local | 7 | Unused private parameter |
| RedundantCast | 6 | Unnecessary type cast |
| RedundantTypeArgumentsOfMethod | 6 | Redundant generic arguments |
| UsingStatementResourceInitialization | 6 | Object initializer in using |
| VariableLengthStringHexEscapeSequence | 6 | Ambiguous hex escape |
| UnusedVariable | 4 | Unused local variable |
| ConditionIsAlwaysTrueOrFalse | 3 | Redundant boolean check |
| EmptyGeneralCatchClause | 3 | Empty catch block |
| FormatStringProblem | 3 | Format string mismatch |
| NullCoalescingAlwaysNotNull | 3 | Redundant `??` |
| RedundantUsingDirective | 3 | Unused using |
| RedundantSwitchExpressionArms | 2 | Unreachable switch arms |
| ReturnTypeCanBeNotNullable | 2 | Return type doesn't need `?` |
| AccessToDisposedClosure | 1 | Accessing disposed variable |
| CollectionNeverUpdated.Global | 1 | Public collection never populated |
| EqualExpressionComparison | 1 | Comparing expression to itself |
| NotAccessedPositionalProperty.Local | 1 | Unused private positional property |
| NotResolvedInText | 1 | Unresolvable symbol in text |
| RedundantLambdaParameterType | 1 | Unnecessary lambda param type |
| UnassignedGetOnlyAutoProperty | 1 | Unassigned get-only property |

### Notes (2,082)
| Rule | Count | Description |
|------|-------|-------------|
| ArrangeObjectCreationWhenTypeEvident | 1,004 | Use `new()` syntax |
| UseCollectionExpression | 304 | Use collection expressions |
| UnusedMember.Global | 173 | Unused non-private member |
| ConvertToPrimaryConstructor | 169 | Use primary constructors |
| UnusedMemberInSuper.Global | 49 | Member not accessed via base |
| UseObjectOrCollectionInitializer | 48 | Use initializer syntax |
| PropertyCanBeMadeInitOnly.Global | 39 | Use `init` accessor |
| UnusedType.Global | 30 | Unused non-private type |
| UnusedParameter.Global | 30 | Unused non-private parameter |
| UseNameOfInsteadOfToString | 29 | Use `nameof()` for enums |
| PropertyCanBeMadeInitOnly.Local | 24 | Use `init` (private) |
| UnusedMethodReturnValue.Global | 23 | Ignored return value |
| MergeIntoPattern | 22 | Merge null checks into patterns |
| MemberCanBePrivate.Global | 15 | Reduce visibility |
| AutoPropertyCanBeMadeGetOnly.Local | 14 | Make get-only (private) |
| RedundantVerbatimStringPrefix | 13 | Unnecessary `@` prefix |
| ClassNeverInstantiated.Global | 11 | Class never created |
| RedundantStringInterpolation | 11 | Unnecessary `$""` |
| ConvertConstructorToMemberInitializers | 9 | Move to field initializers |
| RedundantExplicitParamsArrayCreation | 8 | Redundant params array |
| AutoPropertyCanBeMadeGetOnly.Global | 6 | Make get-only (public) |
| ClassNeverInstantiated.Local | 6 | Private class never created |
| UseAwaitUsing | 6 | Use `await using` |
| MemberCanBePrivate.Local | 5 | Reduce private visibility |
| PreferConcreteValueOverDefault | 5 | Use concrete value not `default` |
| UseUtf8StringLiteral | 5 | Use UTF-8 string literal |
| RedundantTypeDeclarationBody | 3 | Empty `{ }` on type |
| SimplifyLinqExpressionUseAll | 3 | Use `.All()` |
| AsyncMethodWithoutAwait | 2 | Async without await |
| ChangeFieldTypeToSystemThreadingLock | 2 | Use `Lock` type |
| ConvertToLocalFunction | 2 | Lambda → local function |
| MemberCanBeProtected.Global | 2 | Make protected |
| ArrangeRedundantParentheses | 2 | Remove extra parens |
| VirtualMemberNeverOverridden.Global | 2 | Unnecessary virtual |
| ArrangeMissingParentheses | 1 | Add clarifying parens |
| ConvertClosureToMethodGroup | 1 | Lambda → method group |
| MethodHasAsyncOverload | 1 | Use async version |
| NotAccessedField.Global | 1 | Unused field |
| ReplaceWithSingleCallToLastOrDefault | 1 | Simplify LINQ |
| TypeParameterCanBeVariant | 1 | Add variance |
