# OpenAPI error code export: `@hey-api/openapi-ts` facts

Research note for the wayfinder question "can the SDK generator emit a TypeScript const or
enum of error codes from the OpenAPI document?". This is a fact sheet, not a design: every
claim cites a primary source (the installed package, the generator's own docs at the
installed tag, Microsoft docs, or a file in this repo), and the ambiguous ones were settled
by running the installed generator against a hand-written specification.

Verified against the installed `@hey-api/openapi-ts` **0.99.0** (`pnpm-lock.yaml`, engine
`node >= 22.18.0`; release notes: <https://github.com/hey-api/openapi-ts/releases/tag/@hey-api/openapi-ts@0.99.0>).
The repository has since moved to `hey-api/hey-api`; documentation links below point at the
`@hey-api/openapi-ts@0.99.0` tag so they match the installed behaviour.

## Summary

| Question | Answer | Evidence |
| --- | --- | --- |
| Can a `string` `enum` schema become a runtime TS value? | Yes. `@hey-api/typescript` option `enums` selects `'javascript'` (`as const` object + type), `'typescript'` (`enum`), or `'typescript-const'` (`const enum`). Default `false` emits a string-literal union only. | [Section 1](#1-runtime-enum-emission) |
| Are `x-enum-varnames` / `x-enumNames` honoured? | Yes, both, plus `x-enum-descriptions` for member JSDoc. Without them member keys are derived from the value and re-cased (default `SCREAMING_SNAKE_CASE`). | [Section 2](#2-member-names-x-enum-varnames-x-enumnames-x-enum-descriptions) |
| Does an unreferenced `components.schemas.ErrorCode` still generate? | Yes with Wallow's current config. Orphan pruning runs only when `parser.filters` is configured; `packages/sdk/openapi-ts.config.ts` has no `parser` block. | [Section 3](#3-unreferenced-orphan-schemas) |
| Can `ProblemDetails.code` reference it? | Yes on the generator side (`$ref` produces `code?: ErrorCode`). On the API side `code` is not a schema property today: it is written into `ProblemDetails.Extensions["code"]` at runtime, so the document would need a schema transformer or a generator-side patch. | [Section 4](#4-referencing-the-enum-from-problemdetailscode) |
| Alternatives | Build-time OpenAPI emission already exists in the toolchain; Roslyn source generators emit C# only. | [Section 7](#7-alternatives-facts-only) |

## Method

- Installed package inspected at
  `node_modules/.pnpm/@hey-api+openapi-ts@0.99.0_typescript@6.0.3/node_modules/@hey-api/openapi-ts/`
  (`dist/index.d.mts` for option types, `dist/init-*.mjs` for the typescript plugin) and its
  parser dependency `@hey-api/shared` (`dist/index.mjs`, `parseEnum`, `hasFilters`,
  `dropOrphans`).
- Empirical runs: a hand-written OpenAPI 3.1.1 document with one operation whose `404`
  response references `ProblemDetails`, whose `code` property is `$ref
  #/components/schemas/ErrorCode` (a string enum carrying `x-enum-varnames` and
  `x-enum-descriptions`), plus two unreferenced enum schemas (`OrphanCode`, and `OrphanNamed`
  with `x-enumNames` and a `null` member) and one inline enum property. The document was run
  through the installed binary (`bin/run.js -f <config>`) under eleven configurations.
  Nothing was generated into `packages/sdk`.

## 1. Runtime enum emission

The `@hey-api/typescript` plugin's `enums` option controls whether enums are emitted as
types only or as runtime values. Documented values (typescript plugin page at the installed
tag: <https://github.com/hey-api/hey-api/blob/@hey-api/openapi-ts@0.99.0/web/src/content/docs/docs/openapi/typescript/plugins/typescript.mdx?plain=1>):

- `false` (default): enums become string-literal union types.
- `'javascript'`: an `as const` object plus a same-named type (recommended by the docs).
- `'typescript'`: a TypeScript `enum`.
- `'typescript-const'`: a `const enum`.
- Object form `{ enabled, mode, case, constantsIgnoreNull }` for finer control.

The installed type declaration matches (`dist/index.d.mts`): `enums?: boolean | EnumsType |
{ case?: StringCase; constantsIgnoreNull?: boolean; enabled?: boolean; mode?: EnumsType }`
with `EnumsType = 'javascript' | 'typescript' | 'typescript-const'`. The plugin's default
config in `dist/init-*.mjs` is `enums: { case: "SCREAMING_SNAKE_CASE", constantsIgnoreNull:
false, enabled: false, mode: "javascript" }`. The `enumsCase` and
`enumsConstantsIgnoreNull` names that older guides mention were folded into the `enums`
object in v0.78.0 (migration guide:
<https://github.com/hey-api/openapi-ts/blob/main/web/src/content/docs/docs/openapi/typescript/migrating.mdx?plain=1#L411>).

Observed output for `ErrorCode` (values `Identity.ClientNotSuspended`,
`invalid_credentials`, `NETWORK_ERROR`; varnames `ClientNotSuspended`,
`InvalidCredentials`, `NetworkError`):

```ts
// enums: false (default)
export type ErrorCode = 'Identity.ClientNotSuspended' | 'invalid_credentials' | 'NETWORK_ERROR';

// enums: 'javascript'
export const ErrorCode = {
    /** Client is not suspended */
    CLIENT_NOT_SUSPENDED: 'Identity.ClientNotSuspended',
    /** Bad credentials */
    INVALID_CREDENTIALS: 'invalid_credentials',
    /** Network down */
    NETWORK_ERROR: 'NETWORK_ERROR'
} as const;
export type ErrorCode = typeof ErrorCode[keyof typeof ErrorCode];

// enums: 'typescript'
export enum ErrorCode { CLIENT_NOT_SUSPENDED = 'Identity.ClientNotSuspended', /* ... */ }

// enums: 'typescript-const'
export const enum ErrorCode { CLIENT_NOT_SUSPENDED = 'Identity.ClientNotSuspended', /* ... */ }
```

Additional observed facts:

- `'typescript'` mode cannot represent a `null` member: the nullable `OrphanNamed` schema
  stayed a type union in that mode, while `'javascript'` mode emitted `NIL: null` as a member.
- Integer enums also get runtime values in `'javascript'` mode (`IntCode = { 1: 1, 2: 2, 3: 3 }`).
- With the full Wallow plugin set (`client-fetch`, `typescript`, `sdk`,
  `@tanstack/react-query`), the generated `index.ts` barrel exports the enum as a value
  (`export { type ClientOptions, ErrorCode, ... } from './types.gen'`). Wallow's
  `packages/sdk/src/index.ts` does `export * from "./generated"`, so a runtime enum would be
  reachable from the package root without further wiring.

## 2. Member names: `x-enum-varnames`, `x-enumNames`, `x-enum-descriptions`

`x-` prefixed keys are OpenAPI specification extensions; their meaning is defined by the
tool that reads them, not by the specification
(<https://github.com/oai/openapi-specification/blob/main/versions/3.0.4.md?plain=1#L3798>).

The generator reads three of them in `@hey-api/shared` (`parseEnum`, present in both the
3.0.x and 3.1.x parsers). For each enum value at index `i` the intermediate schema gets
`title: xEnumVarnames?.[i] ?? xEnumNames?.[i]` and `description: xEnumDescriptions?.[i]`.
The typescript plugin then uses the title as the member key (re-cased per `enums.case`) and
the description as the member's JSDoc. Precedence: `x-enum-varnames` wins over
`x-enumNames`; with neither, the key is derived from the value itself.

Observed key derivation without varnames (`enums: 'javascript'`, default case):

| Value | Emitted key |
| --- | --- |
| `Identity.ClientNotSuspended` | `IDENTITY_CLIENT_NOT_SUSPENDED` |
| `invalid_credentials` | `INVALID_CREDENTIALS` |
| `NETWORK_ERROR` | `NETWORK_ERROR` |
| `Validation.Error` | `VALIDATION_ERROR` |
| `BusinessRule.Org.Archived` | `BUSINESS_RULE_ORG_ARCHIVED` |
| `x-y` (via `x-enumNames: XY`) | `XY` |
| `1st` (via `x-enumNames: First`) | `FIRST` |

`enums.case: 'PascalCase'` produced `ClientNotSuspended`; `constantsIgnoreNull: true`
dropped the `null` member from the `OrphanNamed` object.

Wallow's committed document (`packages/sdk/openapi/v1.json`, OpenAPI 3.1.1) contains no
`enum` keyword and no `x-enum*` extension anywhere, so nothing in the current pipeline
exercises this path.

## 3. Unreferenced (orphan) schemas

Parser docs at the installed tag
(<https://github.com/hey-api/hey-api/blob/@hey-api/openapi-ts@0.99.0/web/src/content/docs/docs/openapi/typescript/configuration/parser.mdx?plain=1>)
describe `parser.filters.orphans` as "whether to keep resources unreferenced by operations",
default `false`. The installed implementation (`@hey-api/shared`, `parseV3_1_X` and
siblings) qualifies that default:

- Filtering runs only `if (hasFilters(context.config.parser.filters))`.
- `hasFilters` returns `true` only when `orphans === false`, `deprecated === false`, or any
  `operations`/`parameters`/`requestBodies`/`responses`/`schemas`/`tags` include or exclude
  list is non-empty.
- Inside the filter pass, `orphans` defaults to `false` and `dropOrphans` deletes every
  schema, parameter, request body, and response that is neither reachable from an operation
  nor named in an include list (`if (!filters.orphans && operations.size) dropOrphans(...)`).

Consequences, all confirmed by the runs:

- No `parser` block (Wallow's current `packages/sdk/openapi-ts.config.ts`): every component
  schema is emitted, referenced or not. `OrphanCode` and `OrphanNamed` appeared in
  `types.gen.ts` under the default, `'javascript'`, `'typescript'`, and `'typescript-const'`
  configurations.
- Any filter (`filters: { orphans: false }` alone, or `filters: { schemas: { include:
  ['ErrorCode'] } }`) drops the orphans. `ErrorCode` survives because `ProblemDetails`
  references it and the operation's `404` response references `ProblemDetails`.
- `schemas.include` is an allow-list: with `include: ['ErrorCode']` and `orphans: true`, the
  orphans were still dropped.

So an `ErrorCode` component that no operation references is emitted today, and would keep
being emitted unless a future change adds `parser.filters`.

## 4. Referencing the enum from `ProblemDetails.code`

Generator side: a `code` property declared as `{ "$ref": "#/components/schemas/ErrorCode" }`
generated `code?: ErrorCode;` inside `ProblemDetails` under every configuration, with the
`ErrorCode` symbol resolving to the type (default) or to the const/enum plus its type.

API side, current state:

- `packages/sdk/openapi/v1.json` describes `ProblemDetails` with `type`, `title`, `status`,
  `detail`, `instance` only; `ValidationProblemDetails` adds `errors`. There is no `code`.
- The code is attached at runtime as an extension member:
  `api/src/Shared/Wallow.Shared.Api/Extensions/ResultExtensions.cs` sets `Extensions = {
  ["code"] = error.Code }` and `api/src/Wallow.Api/Middleware/GlobalExceptionHandler.cs`
  sets `problemDetails.Extensions["code"] = domainException.Code` (only for
  `DomainException`; `ValidationException` produces `errors`). `api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs`
  adds `api` and `version` the same way via `CustomizeProblemDetails`. The schema generator
  cannot see any of these.
- `api/src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs` registers four document
  transformers and no schema transformer; no `JsonStringEnumConverter` is used anywhere
  under `api/src`.

Facts about the two mechanisms that can put `code` (and a component reference) into the
document:

- ASP.NET Core `Microsoft.AspNetCore.OpenApi` (10.0.x per `api/Directory.Packages.props`)
  supports document, operation, and schema transformers. A transformer can add a component
  with `context.Document.AddComponent(...)`, obtain a schema for a CLR type with
  `context.GetOrCreateSchemaAsync(...)`, and set schema properties and `Extensions`
  (<https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/fundamentals/openapi/customize-openapi.md?plain=1>).
  The same docs describe how enum types surface in the document: an enum annotated with
  `JsonStringEnumConverter` is emitted as a `string` schema with an `enum` list, and enums
  are emitted as component references
  (<https://github.com/dotnet/aspnetcore.docs/blob/main/aspnetcore/fundamentals/openapi/includes/include-metadata10.md?plain=1#L587>).
  `System.Text.Json`'s `JsonStringEnumMemberNameAttribute` (.NET 9+) sets the string emitted
  for a member, which is what would carry dotted or snake_case codes on a C# enum
  (<https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenummembernameattribute?view=net-10.0>).
  Vendor extensions on a schema are written through `Microsoft.OpenApi`'s
  `JsonNodeExtension` wrapper (`IOpenApiExtension` over a `JsonNode`;
  <https://learn.microsoft.com/en-us/dotnet/api/microsoft.openapi.jsonnodeextension>).
- Generator side, `parser.patch` lets the config mutate `schemas`, `operations`, and other
  document sections before parsing (same parser docs page as Section 3), so `code` and
  an `ErrorCode` component could also be injected in `packages/sdk/openapi-ts.config.ts`
  without touching the API. That would make the SDK diverge from the committed snapshot
  that CI compares against (`.github/workflows/openapi-drift.yml`,
  `.github/workflows/openapi-autoregen.yml`).

## 5. Inline enums

An enum declared inline on a property (`inlineCode: { type: string, enum: [p, q] }`) stays an
inline union by default. `parser.transforms.enums: 'root'` hoists it into a named component
(observed: `export const InlineCodeEnum = { P: 'p', Q: 'q' } as const;` and
`inlineCode?: InlineCodeEnum`), which is what makes runtime enum emission apply to it
(parser docs page above).

## 6. Where Wallow's codes live today

- `api/src/Shared/Wallow.Shared.Kernel/Results/Error.cs`: `Error(string Code, string
  Message)` record with static factories; 11 distinct factory codes in use.
- `api/src/Shared/Wallow.Shared.Kernel/Domain/DomainException.cs`: `Code` property;
  subclasses pass string literals to the base constructor (for example
  `api/src/Modules/Identity/Wallow.Identity.Domain/Entities/RegisteredClient.cs`,
  `new BusinessRuleException("Identity.ClientNotSuspended", ...)`).
- Inline literals also exist at the controller level
  (`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/UsersController.cs`,
  `["code"] = "Validation.ReservedRoleName"`).
- Three naming styles coexist: `Identity.ClientNotSuspended` (dotted PascalCase),
  `invalid_credentials` (snake_case, Identity OIDC paths), `NETWORK_ERROR`
  (SCREAMING_SNAKE_CASE).
- No static catalog class (`*Codes`, `*Errors`) exists; codes are string literals at the
  point of use, so there is no single CLR symbol the schema generator could reflect over.

## 7. Alternatives (facts only)

**A `codes.json` emitted by the API at build.** Build-time document generation already
exists: `api/src/Wallow.Api/Wallow.Api.csproj` references
`Microsoft.Extensions.ApiDescription.Server` (10.0.0) with `OpenApiGenerateDocumentsOnBuild`
off by default and `OpenApiDocumentsDirectory` under `obj/openapi/`; the mechanism is
documented at
<https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0#generate-openapi-documents-at-build-time>.
CI drives it through `.github/actions/openapi-document`, commits the result to
`packages/sdk/openapi/v1.json`, and regenerates `packages/sdk/src/generated` on the
`automation/openapi-regen` branch. Nothing in the toolchain produces a separate codes file;
a component schema in the OpenAPI document rides this existing pipeline, whereas a
`codes.json` would need its own emitter, snapshot, and drift check.

**A `dotnet` source generator.** Roslyn source generators add C# source to a compilation
and can read non-C# additional files, but they only emit C#
(<https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md?plain=1#L3>).
A generator could therefore produce a C# enum or catalog from the scattered literals (or
from an additional file), from which the OpenAPI schema would then derive via Section 4; it
cannot produce the TypeScript artefact itself.

**`parser.patch` in the SDK config.** Covered in Section 4: works, but bypasses the
committed document that CI treats as the source of truth.

## Not verified

- Whether `JsonSchemaExporter` (the .NET 9+ schema exporter behind
  `Microsoft.AspNetCore.OpenApi`) can emit anything usable as `x-enum-varnames` from a C#
  enum; its docs describe `TransformSchemaNode` for arbitrary post-processing but say nothing
  about enum member names
  (<https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema>).
- The exact `Microsoft.OpenApi` package version resolved by `Microsoft.AspNetCore.OpenApi`
  10.0.11 in this repo; the `JsonNodeExtension` reference page cited above shows the current
  3.x package. Confirm the type before writing a schema transformer against it.

## Lab artefacts

Configs and outputs live only in the session scratchpad (not committed): a `spec.json`, a
`spec-novarnames.json`, one config per scenario (`a-default`, `b-js`, `c-ts`, `d-ts-const`,
`e-js-filter-include`, `f-js-filter-include-orphans-true`, `g-js-orphans-false`,
`h-js-transform-root`, `i-js-pascal-ignorenull`, `j-js-fullplugins`, `k-novarnames`), and
the `types.gen.ts` each produced. Re-running takes the installed binary and any OpenAPI file:
`node node_modules/@hey-api/openapi-ts/bin/run.js -f <config.mjs>`.
