**status: active**

# Bug Remediation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan
> task-by-task.

**Goal:** Close the five actionable bug-typed beads under epic Wallow-4pwv, and confirm-and-close
the sixth.

**Architecture:** Five independent changes sharing no files — a docfx config flatten plus a built-toc
guard, an oxlint config-inheritance fix plus its tree-wide guard, a stale xUnit assertion, a vitest
preset timeout, and two env-derived E2E endpoints. Each task is self-contained: land them in any
order, commit each separately.

**Tech stack:** docfx 2.78.5, oxlint (oxc toolchain — not eslint), vitest 4 (node + real-Chromium
browser projects), xUnit + AwesomeAssertions + Aspire.Hosting testing, Playwright.

**Rationale for every decision here lives in `docs/plans/2026-07-30/1102-bug-remediation-design.md`.**
Read §1 and §3 of that document before starting Tasks 1 and 3 — two bead descriptions are wrong and
the design doc says how.

**Before you start:**

```bash
cd /Users/traveler/Repos/Wallow
git pull --rebase
bd show Wallow-4pwv          # the epic; children carry measured notes
pnpm --filter @bc-solutions-coder/sdk build   # apps typecheck against dist/
```

The working tree may carry one uncommitted `docfx.json` change (adding `"audits/**"` to the docs
`exclude`). It is correct and unrelated. Keep it; it will ride along with Task 3.

---

## Task 1: Wallow-s7j6 — correct the stale AppHost OIDC assertion

The AppHost is right and the test is wrong. See design doc §3 — do **not** "fix" this by changing
`Program.cs`.

**Files:**

- Modify: `api/tests/Wallow.AppHost.Tests/AppHostEnvironmentWiringTests.cs:54` and the class doc
  comment at lines 7-15
- Read only (do not edit): `api/src/Wallow.AppHost/Program.cs:90-91`

**Step 1: Watch it fail**

```bash
./scripts/run-tests.sh apphost
```

Expected: `WallowWeb_SetsAllRequiredBffEnvironmentVariables` FAILS — expected
`http://localhost:5001`, found `http://localhost:3002`.

If the shorthand `apphost` is not recognised, run `./scripts/run-tests.sh` with no arguments and
read the supported list from the script.

**Step 2: Correct the assertion and add the missing pair half**

In `AppHostEnvironmentWiringTests.cs`, replace line 54:

```csharp
        env.Should().ContainKey("OIDC_ISSUER").WhoseValue.Should().Be("http://localhost:5001");
```

with both assertions — the issuer and the metadata URL are only correct as a pair, and the metadata
URL is currently unpinned:

```csharp
        // The dev issuer is the wallow-auth origin, not the API's: appsettings.Development.json
        // sets AuthUrl=http://localhost:3002 and OpenIddictIssuerResolver echoes it, so the client
        // must EXPECT :3002 while fetching discovery from the API directly on :5001. Assert both —
        // either one alone permits a mismatched pair that would break the real flow.
        env.Should().ContainKey("OIDC_ISSUER").WhoseValue.Should().Be("http://localhost:3002");
        env.Should().ContainKey("OIDC_METADATA_URL").WhoseValue.Should()
            .Be("http://localhost:5001/.well-known/openid-configuration");
```

**Step 3: Correct the class doc comment**

Lines 7-15 claim the values "mirror the containerised values proven in
`docker/docker-compose.test.yml`". They do not — compose uses `:5050` for both because the
containerised topology differs. Replace that sentence with:

```csharp
/// Known-correct target values are the Aspire-local ports set in Wallow.AppHost/Program.cs.
/// They deliberately do NOT match docker/docker-compose.test.yml, which uses :5050 for both the
/// issuer and the metadata URL because the containerised origins differ. Do not "align" them.
```

**Step 4: Format and verify**

```bash
dotnet format api/Wallow.slnx
./scripts/run-tests.sh apphost
```

Expected: PASS. Then run the full suite to confirm nothing else moved:

```bash
./scripts/run-tests.sh
```

**Step 5: Commit**

```bash
git add api/tests/Wallow.AppHost.Tests/AppHostEnvironmentWiringTests.cs
git commit -m "fix(tests): expect the wallow-auth origin as the dev OIDC issuer"
bd close Wallow-s7j6
```

---

## Task 2: Wallow-3q9c — give the node project a real timeout budget

**Files:**

- Modify: `packages/testing/src/vitest-projects.ts` (the `node` project literal, ~line 95)

**Step 1: Reproduce**

```bash
pnpm --filter @bc-solutions-coder/wallow-auth test 2>&1 | tail -20
```

Expected: failures reading `Test timed out in 5000ms` in `src/routes/__root.provider.test.tsx`. If
the machine is idle they may not appear — the failure needs the browser project competing for CPU.
Confirm the cause directly instead:

```bash
grep -n "testTimeout" packages/testing/src/vitest-projects.ts
```

Expected: no match. That is the bug — vitest's 5000 ms default applies to a first import measured at
19 043 ms.

**Step 2: Set the budget**

In `createVitestProjects`, change the `node` project literal from:

```typescript
  const node: VitestNodeProject = {
    test: {
      name: "node",
      environment: "node",
      include: ["src/**/*.test.ts", ...nodeTsxSpecs],
      exclude: [...configDefaults.exclude],
    },
  };
```

to:

```typescript
  const node: VitestNodeProject = {
    test: {
      name: "node",
      environment: "node",
      include: ["src/**/*.test.ts", ...nodeTsxSpecs],
      exclude: [...configDefaults.exclude],
      // A route-root spec's FIRST `await import("./__root")` pays a cold Vite transform of the
      // whole route graph — measured at 19s for wallow-auth's __root.provider.test.tsx, against
      // 1ms for the second test in the same file once the module is cached. Vitest's 5s default
      // fails that import whenever this project competes with the browser project for CPU. The
      // cost is structural (TanStack Router/Start + react-query, NOT the @bc-solutions-coder/ui
      // barrel — swapping to ui subpaths moved it 19043ms -> 18805ms), so the budget belongs in
      // the shared preset. 60s clears the measurement 3x over while still failing a real hang.
      testTimeout: 60_000,
    },
  };
```

**Step 3: Add the field to the type**

`VitestNodeTestConfig` already carries an index signature (`[key: string]: unknown`), so this
compiles as-is. Declare it explicitly anyway so the preset's contract is readable:

```typescript
export interface VitestNodeTestConfig {
  name: string;
  environment: string;
  include: string[];
  exclude: string[];
  /** See the node project literal for why this is 60s and not vitest's 5s default. */
  testTimeout: number;
  [key: string]: unknown;
}
```

**Step 4: Update the module header**

The header block documents the node/browser split. Add one line after the `node` description so the
number is not mysterious later:

```
 *             a 60s testTimeout (see the literal) covering the cold route-graph import.
```

**Step 5: Verify**

```bash
pnpm --filter @bc-solutions-coder/testing build
pnpm --filter @bc-solutions-coder/wallow-auth test
pnpm --filter @bc-solutions-coder/wallow-web test
```

Expected: `wallow-auth` **812/815**, `wallow-web` **558/559** — the counts the Wallow-m5aq.2.14
Wave 1 gate recorded. The residual failures are Wallow-jx7f specs and are **out of scope**; do not
chase them. Any *other* failure means something regressed — stop and investigate.

**Step 6: Commit**

```bash
git add packages/testing/src/vitest-projects.ts
git commit -m "fix(testing): budget the node project for the cold route-graph import"
bd close Wallow-3q9c
```

---

## Task 3: Wallow-jtdg — flatten the docfx toc collision and guard it

Read design doc §1 first. **The bead's headline symptom does not reproduce** — the sidebar renders
today. You are fixing an undefined tie-break, an unreachable API reference, and the absence of a
guard.

**Files:**

- Modify: `docfx.json` (remove one `build.content` entry)
- Delete: `docfx/toc.yml`
- Modify: `docs/toc.yml` (API Reference section)
- Create: `packages/sdk/src/docs-toc.test.ts`

### Step 1: Record the baseline

```bash
rm -rf .docfx/_site
dotnet docfx build docfx.json 2>&1 | grep -cE "DuplicateOutputFiles|Unable to find either"
```

Expected: `4`. (A full `dotnet docfx docfx.json` also runs metadata; the build step alone is enough
here and much faster.)

### Step 2: Write the failing guard

Create `packages/sdk/src/docs-toc.test.ts`. This package is where doc-assertion specs already live
(`query-rule-docs.test.ts`, `bff-pattern-docs.test.ts`, `request-correlation-docs.test.ts`) — follow
their shape. **No YAML dependency**: the workspace has none, and adding one for a guard is not worth
it. `docs/toc.yml` hrefs are one-per-line and extract cleanly with a regex, exactly as the
neighbouring specs do string work on prose.

```typescript
import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * Guard for Wallow-jtdg: `docs/toc.yml` must be the ONE toc emitted at the site root, and every
 * entry in it must survive into the built site.
 *
 * The bug this pins was invisible: two `docfx.json` build.content entries both emitted `toc.json`
 * to the site root, docfx resolved the collision by an undefined tie-break, and the build still
 * exited 0 whichever side won. Measured on this tree, `docs/toc.yml` wins — so the sidebar renders
 * by luck, not by construction. These assertions turn that luck into a contract.
 *
 * Split in two on purpose:
 *  - The CONFIG assertion always runs. It is the one that fails fast in `pnpm test` if a second
 *    root-toc emitter is ever added back.
 *  - The BUILT-SITE assertions are `skipIf`-gated on the artifact existing, matching how
 *    `packages/forms/src/index.test.ts` gates its `dist/` assertions. They arm after
 *    `dotnet docfx build docfx.json` and in CI, which builds the site.
 */

// packages/sdk/src -> repo root
const repoRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const docfxConfigPath: string = resolve(repoRoot, "docfx.json");
const tocSourcePath: string = resolve(repoRoot, "docs/toc.yml");
const builtTocPath: string = resolve(repoRoot, ".docfx/_site/toc.json");

interface DocfxContentEntry {
  files: string[];
  src?: string;
  dest?: string;
  exclude?: string[];
}

interface DocfxConfig {
  build: { content: DocfxContentEntry[]; dest: string };
}

function readDocfxConfig(): DocfxConfig {
  return JSON.parse(readFileSync(docfxConfigPath, "utf8")) as DocfxConfig;
}

/** Every `href: some/path.md` in docs/toc.yml, in source order. */
function readSourceHrefs(): string[] {
  const source: string = readFileSync(tocSourcePath, "utf8");
  return [...source.matchAll(/^\s*href:\s*(\S+\.md)\s*$/gmu)].map((match) => match[1]);
}

/** Every `href` anywhere in the built toc.json tree, flattened. */
function readBuiltHrefs(): string[] {
  interface TocNode {
    href?: string;
    items?: TocNode[];
  }
  const built: TocNode = JSON.parse(readFileSync(builtTocPath, "utf8")) as TocNode;
  const collected: string[] = [];
  const walk = (node: TocNode): void => {
    if (node.href !== undefined) {
      collected.push(node.href);
    }
    for (const child of node.items ?? []) {
      walk(child);
    }
  };
  walk(built);
  return collected;
}

describe("docfx toc configuration", () => {
  it("emits exactly one toc at the site root", () => {
    // An entry emits a root toc when it ships a toc.yml AND its dest is the site root
    // (`.` or absent). Two such entries collide on toc.json and docfx silently drops one.
    const rootTocEmitters: DocfxContentEntry[] = readDocfxConfig().build.content.filter(
      (entry) =>
        entry.files.some((pattern) => pattern.endsWith("toc.yml")) &&
        (entry.dest === undefined || entry.dest === "."),
    );

    expect(
      rootTocEmitters.map((entry) => entry.src ?? "<repo root>"),
      "exactly one build.content entry may emit toc.json at the site root; a second one collides " +
        "and docfx picks a winner by an undefined rule (Wallow-jtdg)",
    ).toEqual(["docs"]);
  });

  it("lists every source href with a .md extension", () => {
    expect(readSourceHrefs().length).toBeGreaterThan(30);
  });
});

const builtSiteIsMissing: boolean = !existsSync(builtTocPath);

describe.skipIf(builtSiteIsMissing)("built docs toc", () => {
  it("carries every entry from docs/toc.yml", () => {
    const built: Set<string> = new Set(readBuiltHrefs());
    const missing: string[] = readSourceHrefs()
      .map((href) => href.replace(/\.md$/u, ".html"))
      .filter((href) => !built.has(href));

    expect(
      missing,
      "every docs/toc.yml entry must survive the build; a missing one means the site-root toc " +
        "collision came back (Wallow-jtdg)",
    ).toEqual([]);
  });

  it("reaches the generated API reference", () => {
    expect(
      readBuiltHrefs().some((href) => href.startsWith("api/") && href.endsWith(".html")),
      "the generated .NET API reference must be reachable from the sidebar",
    ).toBe(true);
  });
});
```

**Step 3: Run it and watch the config assertion fail**

```bash
pnpm --filter @bc-solutions-coder/sdk test docs-toc
```

Expected: `emits exactly one toc at the site root` FAILS — got `["docs", "docfx"]`, wanted
`["docs"]`. The built-site block passes already (that is the point of §1: the site is currently
correct by luck).

### Step 4: Flatten the config

In `docfx.json`, delete the second `build.content` entry entirely:

```json
      {
        "files": ["toc.yml"],
        "src": "docfx"
      },
```

Leave the first entry (`src: "docs"`, `dest: "."`) and the third (the API metadata) untouched.

### Step 5: Delete the dead toc

```bash
git rm docfx/toc.yml
```

Both its hrefs point at directories with no toc; it is the source of the two "Unable to find" warnings.

### Step 6: Wire the generated API reference into the sidebar

This closes the half of the bead that is not in its description: `.docfx/api/toc.yml` is 109 KB of
generated namespaces and nothing links to it.

`docs/toc.yml` currently ends with a hand-written section:

```yaml
- name: API Reference
  items:
    - name: Service Accounts
      href: api/service-accounts.md
```

Extend it so the generated reference is reachable alongside the hand-written page:

```yaml
- name: API Reference
  items:
    - name: Service Accounts
      href: api/service-accounts.md
    # The generated .NET reference. docfx emits its own toc beside the metadata output
    # (.docfx/api/toc.yml, one entry per namespace); this href hands the sidebar off to it.
    - name: .NET API Reference
      href: ../.docfx/api/toc.yml
```

If docfx rejects that relative href, the alternative is to point at the metadata output's index
(`href: api/index.md` with a `topicHref`), or to give the metadata entry its own `dest` and
reference the emitted toc from there. **Verify empirically in Step 7 and use whichever produces a
reachable `api/` node** — do not leave this step half-applied on the assumption it worked.

### Step 7: Rebuild and verify the warnings are gone

```bash
rm -rf .docfx/_site
dotnet docfx build docfx.json 2>&1 | grep -E "DuplicateOutputFiles|Unable to find either|warning\(s\)"
```

Expected: **zero** `DuplicateOutputFiles` lines and **zero** `Unable to find either` lines.

One unrelated warning survives and is expected: an `InvalidBookmark` at
`docs/integrations/integration-cookbook.md(25,1)`. It has its own bead — leave it.

**Step 8: Arm the guard**

```bash
pnpm --filter @bc-solutions-coder/sdk test docs-toc
```

Expected: all four assertions PASS, including `reaches the generated API reference`.

**Step 9: Eyeball the rendered page**

```bash
open .docfx/_site/integrations/bff-pattern.html
```

Confirm the full sidebar renders and the API Reference section is present and navigable.

**Step 10: Commit**

```bash
git add docfx.json docs/toc.yml packages/sdk/src/docs-toc.test.ts
git rm --cached docfx/toc.yml 2>/dev/null || true
git commit -m "fix(docs): make docs/toc.yml the single site-root toc and guard it"
bd close Wallow-jtdg
```

---

## Task 4: Wallow-i3hr — make the nested oxlint configs inherit

Read design doc §2. Adding `extends` alone surfaces **981 diagnostics** and `pnpm lint` runs
`--deny-warnings`, so all 981 are failures. ~940 are structural noise the root config should never
have applied to a component library; ~33 are in real source and ~23 of those disappear with the same
`no-magic-numbers` exemption apps already have. Budget for the whole task, not just the one-line
config change.

**Files:**

- Modify: `.oxlintrc.json` (root — the `overrides` array)
- Modify: `packages/ui/.oxlintrc.json`, `packages/forms/.oxlintrc.json`
- Modify: ~10 source files (list in Step 5)
- Modify: `packages/sdk/src/oxlint-guardrails.test.ts`

### Step 1: Confirm the hole

```bash
cat packages/ui/.oxlintrc.json packages/forms/.oxlintrc.json
```

Expected: neither has `extends`. Compare with `scripts/fork-smoke/.oxlintrc.json`, which does and is
the correct model.

### Step 2: Widen the root test override to stories

In `.oxlintrc.json`, the last override is `{"files": ["**/*.test.*"], ...}`. Change its `files` to
cover stories, which `packages/ui` executes as test cases through the `storybook` vitest project
(`.claude/rules/TESTING.md`):

```json
      "files": ["**/*.test.*", "**/*.stories.tsx"],
```

### Step 3: Add the component-library override

Append a new entry to the root `overrides` array. This must **narrow by construction** — two named
rules off for test/story files only, never a category:

```json
    {
      "files": [
        "packages/ui/**/*.{test,stories}.{ts,tsx}",
        "packages/forms/**/*.{test,stories}.{ts,tsx}"
      ],
      "rules": {
        "unicorn/prefer-dom-node-dataset": "off",
        "react/jsx-max-depth": "off"
      }
    },
    {
      "files": ["packages/ui/**/*.{ts,tsx}", "packages/forms/**/*.{ts,tsx}"],
      "rules": {
        "no-magic-numbers": ["warn", { "ignore": [0, 1] }]
      }
    },
```

Why each:

- `unicorn/prefer-dom-node-dataset` (443 hits) — `packages/ui/CLAUDE.md` makes
  `getAttribute("data-*")` the documented way to assert component state.
- `react/jsx-max-depth` (378 hits) — composite Base UI part trees nest by design; the rule stays on
  at depth 2 for production source, where `packages/forms/CLAUDE.md` records honouring it.
- `no-magic-numbers` with `ignore: [0, 1]` (23 of the 33 src hits) — identical to the exemption
  `apps/wallow-web`, `apps/wallow-auth` and `apps/examples` already carry in the same file. This is
  consistency, not a new concession.

### Step 4: Add `extends` to both nested configs

`packages/ui/.oxlintrc.json` and `packages/forms/.oxlintrc.json` become:

```json
{
  "$schema": "../../node_modules/oxlint/configuration_schema.json",
  "extends": ["../../.oxlintrc.json"],
  "rules": {
    "react/jsx-props-no-spreading": "off"
  }
}
```

### Step 5: Measure and fix what is left

```bash
pnpm exec oxlint packages/ui packages/forms 2>&1 | tail -5
```

Expected: roughly **10** diagnostics, down from 981. The remainder are genuine:

| File                                                     | Rule                          |
| -------------------------------------------------------- | ----------------------------- |
| `packages/ui/src/components/context-menu/context-menu.tsx:24` | `no-duplicate-imports`   |
| `packages/ui/src/components/autocomplete/autocomplete.tsx:5,28` | `no-duplicate-imports` (x2) |
| `packages/ui/src/components/form/form.tsx:7`             | `no-duplicate-imports`        |
| `packages/ui/src/core/cn.ts:13`                          | `typescript/array-type`       |
| `packages/forms/src/core/errors.ts:20`                   | `typescript/array-type`       |
| `packages/ui/vite.config.ts:60`                          | `no-continue`                 |
| `packages/ui/.storybook/preview.tsx:29`                  | `prefer-named-capture-group`  |
| `packages/forms/vitest.setup.ts:26`                      | `unicorn/prefer-query-selector` |
| `packages/forms/src/fields/checkbox-field.tsx:86`        | `react/jsx-max-depth`         |

Fix each properly — merge the duplicate imports, switch `ReadonlyArray<T>` to `readonly T[]`, name
the capture group, use `querySelector`. For `checkbox-field.tsx:86`, follow the precedent
`packages/forms/CLAUDE.md` records for `SelectField`: split the tree into one component per nesting
level. Do **not** add per-file disables.

Three of the original 981 were errors rather than warnings; two live in files the Step 3 override now
covers (`otp-field.stories.tsx`, `index.test.ts`) — confirm they are gone rather than assuming it.
`packages/forms/src/core/query-facade.test.ts:127` (`oxc/no-map-spread`) is not covered by either new
override; fix it.

### Step 6: Verify the packages still pass their own suites

Config changes cannot break tests, but the source edits in Step 5 can:

```bash
pnpm --filter @bc-solutions-coder/ui test
pnpm --filter @bc-solutions-coder/forms test
pnpm lint
```

Expected: green, and `pnpm lint` clean across the workspace.

### Step 7: Widen the guardrail spec to every config

`packages/sdk/src/oxlint-guardrails.test.ts` reads only the root `.oxlintrc.json` (see its
`oxlintConfigPath` at the top). Nested configs are invisible to it, which is why it could not catch
this and does not cover `scripts/fork-smoke/.oxlintrc.json` either.

Add a describe block that discovers every `.oxlintrc.json` in the tree (excluding `node_modules`) and
asserts each non-root config either declares `extends` pointing at the root, or re-declares a rule
minus specific entries — never switches a rule or category off wholesale:

```typescript
describe("nested oxlint configs", () => {
  it("every non-root config inherits the root", () => {
    // Discover with a glob rooted at repoRoot, excluding **/node_modules/**.
    // For each: expect(config.extends).toContain(<relative path to root config>)
    // and expect(config.categories).toBeUndefined() — a nested config that redeclares
    // categories has silently detached from the root's severity baseline.
  });
});
```

Write the real assertions; the block above is the shape, not the code. The existing file already has
the config-reading and temp-tree helpers you need — reuse them rather than adding new ones.

### Step 8: Verify the guard catches the original bug

Temporarily delete `"extends"` from `packages/ui/.oxlintrc.json`, run the spec, confirm it FAILS,
then restore. A guard never observed failing is not a guard.

```bash
pnpm --filter @bc-solutions-coder/sdk test oxlint-guardrails
```

### Step 9: Commit

Two commits — the config fix and the source cleanup are separately reviewable:

```bash
git add .oxlintrc.json packages/ui/.oxlintrc.json packages/forms/.oxlintrc.json
git commit -m "fix(lint): inherit the root oxlint config in ui and forms"

git add packages/ui/src packages/forms/src packages/ui/vite.config.ts \
        packages/ui/.storybook/preview.tsx packages/forms/vitest.setup.ts \
        packages/sdk/src/oxlint-guardrails.test.ts
git commit -m "fix(lint): clear the diagnostics the inherited config surfaced"
bd close Wallow-i3hr
```

---

## Task 5: Wallow-ll6c — env-derive the two compose-pinned E2E endpoints

Design doc §5. **Part 3 of the bead is stale** — the dev passwordless rate limiter is already raised
to 1000 in `appsettings.Development.json`. Do not touch it.

**Files:**

- Modify: `apps/wallow-auth/e2e/mailpit.ts:20`
- Modify: `apps/wallow-auth/e2e/logout.spec.ts:21`

**Step 1: Reproduce the Aspire-mode failure**

```bash
pnpm backend        # Aspire: API on :5001, wallow-auth on :3002
pnpm --filter ./apps/wallow-auth exec playwright test magic-link.spec.ts logout.spec.ts
```

Expected: `magic-link` fails with `ECONNREFUSED 127.0.0.1:8035`; `logout` fails to find
`logout-return-link`.

**Step 2: Make the Mailpit default overridable per mode**

`mailpit.ts:20` is currently:

```typescript
const MAILPIT_URL: string = process.env.E2E_MAILPIT_URL ?? "http://127.0.0.1:8035";
```

The override already exists; the problem is the default. Keep `E2E_MAILPIT_URL` as the explicit knob
and make the fallback follow the same signal the Playwright configs already use — `E2E_BASE_URL` set
means the containerised stack:

```typescript
/**
 * Compose publishes Mailpit's HTTP API on 127.0.0.1:8035 (container :8025); a bare Aspire run
 * (`pnpm backend`) delivers to the standing Mailpit on :8025. `E2E_BASE_URL` is the same signal
 * playwright.config.ts uses to tell the two modes apart — set means an externally-supplied stack.
 *
 * The host is 127.0.0.1, not `localhost`: compose publishes IPv4-only, and `localhost` resolves to
 * IPv6 `::1` first on many hosts, where the connection is refused. Do not "simplify" it.
 */
const COMPOSE_MAILPIT_URL: string = "http://127.0.0.1:8035";
const LOCAL_MAILPIT_URL: string = "http://127.0.0.1:8025";

const MAILPIT_URL: string =
  process.env.E2E_MAILPIT_URL ??
  (process.env.E2E_BASE_URL === undefined ? LOCAL_MAILPIT_URL : COMPOSE_MAILPIT_URL);
```

Update the module doc comment above it to describe both modes — it currently documents only compose.

**Step 3: Derive the allowed redirect origin**

`logout.spec.ts:21` is currently:

```typescript
const ALLOWED_REDIRECT_URI = "http://localhost:5051/after-logout";
```

The allow-listed value is whatever the API has configured as its `AuthUrl`, which
`OpenIddictRedirectUriValidator` adds unconditionally. That is `:5051` under compose and `:3002`
under Aspire:

```typescript
/**
 * The allow-listed origin is the API's own configured AuthUrl, which
 * OpenIddictRedirectUriValidator adds to the allow-list unconditionally — :5051 under
 * docker-compose.test.yml, :3002 under a bare Aspire run. E2E_AUTH_ORIGIN overrides both.
 */
const AUTH_ORIGIN: string =
  process.env.E2E_AUTH_ORIGIN ??
  (process.env.E2E_BASE_URL === undefined ? "http://localhost:3002" : "http://localhost:5051");

const ALLOWED_REDIRECT_URI = `${AUTH_ORIGIN}/after-logout`;
```

Update the spec's header comment, which currently states the `:5051` origin as a fixed fact.

**Step 4: Verify BOTH modes** — this is the whole point of the bead

Local (Aspire):

```bash
pnpm backend
pnpm --filter ./apps/wallow-auth test:e2e
```

Containerised:

```bash
./scripts/e2e.sh
```

Both must pass. A change that fixes local and breaks container is a regression, not a fix.

**Step 5: Update the bead and commit**

```bash
git add apps/wallow-auth/e2e/mailpit.ts apps/wallow-auth/e2e/logout.spec.ts
git commit -m "fix(e2e): derive mailpit and redirect origins from the run mode"
bd close Wallow-ll6c
```

---

## Task 6: Wallow-gigs — confirm and close

No code. Both filed symptoms are already fixed in `apps/wallow-auth/e2e/global-setup.ts` (design
doc §6).

**Step 1: Cold-start run**

```bash
rm -rf apps/wallow-auth/node_modules/.vite     # force the cold Vite pre-bundle the bead describes
pnpm backend
pnpm --filter ./apps/wallow-auth test:e2e
```

Expected: no readiness-timeout failures. The warm-up absorbs the cold compile.

**Step 2: Prove the hijack guard fires**

Start a dev server pointed at a different API, then run the suite against it:

```bash
WALLOW_API_INTERNAL_URL=http://localhost:9999 pnpm --filter ./apps/wallow-auth dev &
pnpm --filter ./apps/wallow-auth test:e2e
```

Expected: global setup throws `the app under test is proxying to the WRONG API` with the `lsof`
hint — the guard working, not a failure. Kill the stray server afterwards.

**Step 3: Close**

```bash
bd note Wallow-gigs "Confirmed by a cold-start local-mode run and a deliberate wrong-API hijack; global-setup.ts handles both."
bd close Wallow-gigs
```

---

## Finishing up

```bash
bd epic status                    # Wallow-4pwv should show all children closed
bd dep tree Wallow-4pwv
bd close Wallow-4pwv
pnpm check                        # format:check + lint + typecheck + test + build + check:exports
./scripts/run-tests.sh
git pull --rebase && bd dolt push && git push
git status                        # must read "up to date with origin"
```

Work is not complete until `git push` succeeds (`CLAUDE.md` → Session Completion).

**Still open after this epic, deliberately:** the `integration-cookbook.md:25` bookmark bug (its own
bead, discovered-from Wallow-jtdg), and the `apps/wallow-web/e2e/global-setup.ts` gap noted in design
doc §6 — file that one only if wallow-web grows a backend-dependent spec.
