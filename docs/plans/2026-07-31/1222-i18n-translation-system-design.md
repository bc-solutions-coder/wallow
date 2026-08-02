**status: active**

# Translation System Design

One message catalog in the repo. Three consumers: the two React apps, and the .NET API for
email. No runtime catalog download, no second translation workflow, and adding a language
means adding one file.

## Goals

- **One implementation.** A developer learns a single way to write a translatable string.
- **Lightweight client.** A visitor never downloads a full catalog. Only the messages the
  rendered code actually references ship.
- **Translated email.** The API renders email in the recipient's language.
- **Cheap to extend.** Adding a locale is a file, not a refactor.

## Non-goals

- Translating user-authored content (announcements, inquiry bodies). That needs per-locale
  rows across module schemas and solves a different problem. Excluded deliberately.
- Locale segments in URLs (`/es/dashboard`). See [Locale resolution](#locale-resolution).

## Current state

The repo has no i18n. No `i18next`, no `.resx`, no `CultureInfo`-driven localization;
`System.Globalization` appears only for `InvariantCulture` formatting. English is hardcoded in:

| Surface | Volume |
| --- | --- |
| `apps/wallow-web/src` route and feature components | ~69 strings |
| `apps/wallow-auth/src` | ~32 strings |
| `SimpleEmailTemplateService` (C# `switch` of HTML templates) | 8 templates |
| `Error` / `BusinessRuleException` messages, surfaced as RFC 7807 `detail` | 280 distinct codes |

`packages/ui` and `packages/forms` hold essentially no copy (~1 string between them). Neither
shared package needs i18n coupling — a significant simplification.

Four existing pieces carry weight in this design:

- **`Error(string Code, string Message)`** in `api/src/Shared/Wallow.Shared.Kernel/Results/Error.cs`.
  Every backend error already has a stable machine code. That is a translation key for free.
- **`ISettingsService`** — tenant and user settings over Postgres, cached in Valkey, with a
  reflection-driven registry of defaults. A `locale` user setting needs no migration.
- **`api/branding.json`** — `packages/styles/src/branding.ts` imports it directly while C#
  binds it to `BrandingOptions`. The repo already ships one JSON file read by both toolchains.
- **`packages/query`** — the "one module-global instance, lint-enforced" facade. Paraglide's
  runtime has the same failure mode and gets the same treatment.

## Architecture

### Catalog layout

```
messages/
  en.json                    # base locale, source of truth
  es.json
project.inlang/settings.json # locale list, compiler settings
```

Catalog keys are namespaced by surface: `app.*` for UI chrome, `email.*` for email copy,
`error.*` for backend error codes.

### `packages/i18n` (new)

Paraglide JS in its documented "Pattern 2" shape: one package compiles the shared catalog,
every consumer imports from it.

```
packages/i18n/
  src/paraglide/     # generated — tree-shaken typed message functions
  src/error-codes.ts # Error.Code -> message map
  src/index.ts
```

Exports `./messages` and `./runtime`. The package fills the role `packages/query` fills for
react-query: a single module-global runtime, so locale state cannot desync across duplicate
copies. A root `.oxlintrc.json` `no-restricted-imports` entry bans direct
`@inlang/paraglide-js` imports outside this package, matching the existing rule for
`@tanstack/react-query`.

Paraglide compiles each message into a typed ESM function, so the bundler tree-shakes unused
ones and route-level code splitting splits translations for free. It runs as a Vite plugin or
CLI with no Babel, which matters on this repo's oxc toolchain.

### The API as third consumer

A `JsonMessageCatalog` in `Wallow.Shared.Infrastructure` reads the same `messages/*.json`,
rendering through the `MessageFormat` NuGet package (v8, ~3.4M downloads, actively maintained).
The csproj copies the catalogs in as `Content`.

Reading the catalog directly, rather than maintaining `.resx` alongside it, is what keeps this
a single implementation. `.resx` would mean a second format, a second extraction step, and two
places for a translator to miss a string.

## Locale resolution

Strategy chain: **cookie → `Accept-Language` → base locale**. No locale segment in the URL.

`wallow-web` is an authenticated dashboard, so URL prefixes buy no SEO and would churn
`routeTree.gen.ts`, every `Link`, and all three Playwright suites. The cost is that a shared
link carries no language and the public landing page cannot be indexed per-locale — accepted.

The account setting is the durable record; the `wallow_locale` cookie is its cache. The cookie
is written on login from the account setting, and on any settings change.

### Request-scoped SSR locale

Each app's `src/app/start.ts` gains a `localeMiddleware` beside the existing `sdkMiddleware`.
It resolves the cookie and runs the render inside `AsyncLocalStorage` with
`overwriteGetLocale`.

This is the correctness-critical piece. Paraglide's runtime is module-global, so without
request-scoped storage two concurrent SSR renders overwrite each other's locale and a user
sees another user's language. It is the same hazard `start.ts` already documents for the
per-request SDK, and Paraglide's own docs prescribe `AsyncLocalStorage` as the fix.

`__root.tsx` sets `<html lang>` from the resolved locale.

## Preference storage

`IdentitySettingRegistry` gains one field:

```csharp
public static readonly SettingDefinition<string> Locale =
    new("identity.locale", "en", "Preferred language");
```

`SettingRegistryBase` discovers definitions by reflection over static readonly fields, and the
settings table is generic key/value, so this needs no schema change. Reads and writes go
through the existing `IdentitySettingsController`.

`CurrentUserResponse` gains `Locale`, so `packages/auth`'s existing `currentUserQuery` carries
the preference at no extra request.

## Email

`IEmailTemplateService.RenderAsync` takes a locale. Inside `SimpleEmailTemplateService` the
layout HTML stays in C# — it is markup, not copy — while the copy moves to `messages/*.json`
under `email.*`.

**The recipient's locale, not the actor's.** Email fires from Wolverine event handlers, so a
handler must look up the recipient's `identity.locale` rather than inheriting the locale of
whoever triggered the event. An admin inviting a Spanish-speaking user must send Spanish. This
is the most likely bug in the whole design and deserves a dedicated test.

Fallback order: recipient setting → tenant default → base locale.

## Backend errors

`Error.Code` becomes the translation key. `packages/i18n` exports `translateErrorCode(code)`
backed by an explicit `Record<string, () => string>` map, because Paraglide compiles messages
to functions and offers no runtime key lookup. That map ships whole rather than tree-shaking —
an accepted cost for one small module.

`packages/forms/src/core/server-error.ts` tries the code first and falls back to the server's
`detail`, so an untranslated code degrades to English rather than to blank.

This needs an audit pass. Some errors use generic codes such as `Validation.Error`, which
carry no meaning to translate against and must be made specific first.

## Verification

- **Catalog parity test** in `pnpm check`: every `en.json` key exists in every other locale,
  and no locale carries orphaned keys.
- **Cross-toolchain test**: a .NET test asserting the C# catalog reader resolves the same keys
  the frontend compiles. This seam is the one most likely to drift.
- **Request-isolation test**: concurrent SSR renders with different cookies return different
  languages.
- **Recipient-locale test**: an event triggered by an `en` actor for an `es` recipient produces
  Spanish email.
- **Lint rule**: direct `@inlang/paraglide-js` imports fail outside `packages/i18n`.

`packages/ui` story coverage is unaffected, since the component library holds no copy.

## Adding a language

1. Add `messages/fr.json`.
2. Add `"fr"` to `project.inlang/settings.json`.

Both apps and the email renderer pick it up on the next build.

`messages/*.json` should **not** get the `merge=ours` treatment `api/branding.json` has in
`.gitattributes`. A fork wants upstream's new keys to merge in, and fork-added locales are new
files that cannot conflict.

## Trade-offs accepted

**Paraglide bundles every locale inside each used message function.** Payload scales with
`used messages × locales`, not `all messages × 1`. At ~100 strings and a handful of locales
that is a few KB, and switching locale needs no network request at all. Past roughly 20 locales
this inverts, and the fix is Paraglide's experimental locale-splitting option.

**No runtime key lookup.** Any dynamic key needs an explicit map, as the error-code case shows.

**Smaller ecosystem than i18next**, and Paraglide 2.x moves quickly.

**The bulk of the work is mechanical extraction** of ~100 call sites, not architecture. The
architecture is roughly a day; the extraction is the long tail.

## Alternatives rejected

| Option | Why not |
| --- | --- |
| `react-i18next` | Runtime dictionary lookup ships the whole namespace JSON for the active locale. Benchmarks put it at ~205 KB against Paraglide's ~47 KB for 5 locales and 100 used messages. Directly contradicts the lightweight requirement. |
| Lingui | Also compile-time with a ~2 KB runtime and proper ICU, but its macros want a Babel or SWC transform — a build layer this repo has deliberately avoided by standardizing on oxc. |
| `.resx` + `IStringLocalizer` | Idiomatic .NET with good tooling, but a second catalog format and a second translation workflow. Defeats the single-implementation goal. |
| Node email-rendering service | Most consistent — emails would use the compiled Paraglide messages directly — but adds a service and deployment surface for eight templates. |
| DB-backed catalogs | Runtime-editable and tenant-overridable, but the client must fetch catalogs at runtime and compile-time key safety is lost. |

## References

- [Paraglide JS](https://github.com/opral/paraglide-js) · [monorepo setup](https://github.com/opral/paraglide-js/blob/main/docs/monorepo.md) · [strategies](https://github.com/opral/inlang-paraglide-js/blob/main/docs/strategy.md) · [TanStack Start example](https://github.com/opral/paraglide-js/tree/main/examples/tanstack-start)
- [Paraglide vs react-i18next](https://github.com/opral/paraglide-js/blob/main/docs/paraglide-vs-react-i18next.md)
- [TanStack Router i18n guide](https://tanstack.com/router/latest/docs/guide/internationalization-i18n)
- [ICU MessageFormat guide](https://phrase.com/blog/posts/guide-to-the-icu-message-format/)
