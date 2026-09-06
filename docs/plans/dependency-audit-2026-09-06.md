# Dependency audit, 2026-09-06

## Implemented follow-up: Browserslist security update

Updated the lockfile from Browserslist 4.28.6 to 4.28.9 and refreshed its five supporting browser-data/update dependencies. No manifest, catalog, or override changes were needed. The resulting `pnpm audit --json` reports zero vulnerabilities at every severity, superseding the earlier Browserslist findings below. Frozen installation passes; all 32 workspace build/typecheck tasks pass, with 10 cache hits. Changes remain local and uncommitted.

## Implemented follow-up: Hey API and TypeScript 7

After this audit, the approved generator migration was implemented locally:

- sdk and api-errors now pin `@hey-api/openapi-ts` to `0.0.0-next-20260824173136` and use the shared TypeScript 7.0.2 catalog.
- Removed the TS6 catalog and its lockfile dependency. Regenerated four changed output files; `git diff -w` confirms generated code changes are whitespace-only.
- Replaced the global js-yaml 4 override with `@hey-api/json-schema-ref-parser>js-yaml: 5.4.1`. The prerelease pins vulnerable 5.2.0; the targeted override fixes its two advisories without forcing other consumers onto a new major.
- Both generators pass and produce repeatable output. The drift script passed using a temporary Git index containing regenerated files, leaving the real index unchanged.
- Both generator packages report TypeScript 7.0.2. Their 1,236 tests passed, including a repeat after the final YAML override. Workspace build/typecheck completed 32 tasks, and workspace tests completed 27 tasks, including browser suites, with normal Turbo cache reuse.
- Formatting, application/test lint, manifest/dependency/environment checks, package export checks, and the external consumer runtime/typecheck passed. Frozen lockfile installation passed.
- Final npm audit retains the two pre-existing high-severity Browserslist advisories; no js-yaml advisory remains. No backend end-to-end or deployed-container verification was run for this generator-only change.

The inventory and recommendations below record the pre-migration audit baseline; this follow-up supersedes its Hey API, TS6, and js-yaml entries.

## Scope and evidence

Reviewed every external dependency in the root package.json, apps/*/package.json, packages/*/package.json, and scripts/fork-smoke/package.json; resolved pnpm catalogs and compared the committed lockfile to live npm registry metadata. Excluded internal workspace/file packages and lint fixtures. The table includes 49 external npm package names, the js-yaml override, and pnpm. Peer ranges are included in declared versions; these are compatibility promises, not installed versions.

Baseline is origin/main `994c38dc9056c5829a26f1129e809066569961b4`, including merged Dependabot PRs #217 and #218. Local JavaScript dependency manifests and lockfile match that baseline; local sdk/api-errors package publication versions lag main, which does not change external dependency inventory. Existing unrelated documentation edits were left alone. No dependency versions were changed by this review, and upgrade compatibility has not been proven by builds or tests.

See [the full .NET audit](dotnet-dependency-audit-2026-09-06.md) for central NuGet pins, SDKs, tools, and C# recommendations.

## Priority order

1. Refresh Browserslist in the lockfile to at least 4.28.7. `pnpm audit --json` returned **two high-severity advisories**, both for locked Browserslist 4.28.6: [unbounded memory growth](https://github.com/advisories/GHSA-c83g-rgw3-j3cx) and [untrusted custom stats crash/prototype write](https://github.com/advisories/GHSA-73wf-gq98-2v4g). The dependency paths run through TanStack/Babel and Storybook. This establishes vulnerable dependency presence, not that those inputs are reachable in the deployed app. Prefer an ordinary transitive lockfile refresh before adding an override, then rerun the audit.
2. Update local Node from observed 24.11.1 to **24.20.0**, the latest 24.x LTS in the [official release index](https://nodejs.org/dist/index.json). `.nvmrc`, app containers, and most CI already select major 24; those moving selectors do not prove an existing installation/image contains the latest patches. Keep Node 24 LTS for now. Node 26 is Current until its scheduled October LTS transition. [Release schedule](https://github.com/nodejs/Release).
3. Apply compatible JS patches/minors in coordinated groups, as listed below. Refresh the lockfile as well as manifest floors. Keep TanStack's exact catalog pins coordinated across apps, package peers, and the standalone fork-smoke consumer.
4. Address the .NET Aspire SDK/hosting version gap and update compatible NuGet families in separate batches. Keep .NET/EF on stable 10.x.
5. Schedule pnpm 12, Redis 6, iron-webcrypto 2, and Nitro beta changes individually. Hold Vitest 5 until Storybook supports it.

## Compatibility decisions

- **Vitest 5 is not a clean upgrade today.** [Storybook addon 10.6.0 metadata](https://registry.npmjs.org/@storybook%2Faddon-vitest/10.6.0) allows Vitest 3/4 and browser-playwright 4, not 5. Use Vitest/browser-playwright 4.1.11 together. The [Vitest 5 migration guide](https://vitest.dev/guide/migration/) also changes mock clearing and project inheritance defaults; eventually run all browser and unit tests after migration.
- **TypeScript 7 migration is available through Hey API next.** Stable openapi-ts 0.99.0 still needs the existing TS6 generator exception, but the maintainer confirms in [issue #4235](https://github.com/hey-api/hey-api/issues/4235) that next removes the TypeScript dependency. On September 6, `pnpm view @hey-api/openapi-ts@next version dependencies peerDependencies --json` returned `0.0.0-next-20260824173136`, with no TypeScript dependency or peer. Pin that exact candidate in sdk and api-errors rather than the moving next tag; move both to the tooling TS7 catalog, run both generators, inspect generated API changes, and run package/consumer checks before removing tooling-tsc6. This corrects the original stable-only recommendation. Compatibility with Wallow generators has not yet been tested. The fork-smoke consumer separately declares ^5.6.0 and should be reviewed during compiler alignment.
- **Redis crosses two majors.** Current resolved redis is 4.7.1, not the manifest floor 4.7.0. Latest is 6.2.1. The SDK publishes a ^4.7.0 peer contract, so update consumers and SDK compatibility together. Review [the v4-to-v5 migration](https://github.com/redis/node-redis/blob/master/docs/v4-to-v5.md) and subsequent v6 changes, then exercise reconnects, expiration, and session persistence.
- **iron-webcrypto 2 is a session compatibility change.** [Release notes](https://github.com/brc-dd/iron-webcrypto/releases/tag/v2.0.0) document breaking API changes. Verify old sealed cookies can still be read, or explicitly plan session invalidation.
- **pnpm 12 needs its own verification.** Latest [12.3.4](https://github.com/pnpm/pnpm/releases/tag/v12.3.4) is available; 11.25.0 is the incremental option. Validate catalogs, overrides, allowBuilds, frozen installs, package publishing, and container install steps with the selected major.
- **Nitro's latest tag is a beta.** Do not mistake npm's latest tag for a stable release. The latest tag is 3.0.260903-beta, while the non-prerelease 3.0.0 entry is deprecated and has a Vite 7 peer. Keep this upgrade separate from routine Vite patches/minors and exercise SSR startup and deployment.

## JavaScript inventory

Versions are live npm `dist-tags.latest`, except the recommendation deliberately retains compatible majors where stated. Every package name links to the registry record used for the comparison. A dash means no importer version, such as pnpm or a transitive override, not that the package is unused. The standalone fork-smoke directory has no importer in the root lockfile.

| Package | Declared versions | Locked direct versions | Latest tag | Recommendation | Reason |
| --- | --- | --- | --- | --- | --- |
| [@arethetypeswrong/cli](https://registry.npmjs.org/%40arethetypeswrong%2Fcli) | ^0.18.5 | 0.18.5 | 0.18.5 | 0.18.5 | Already current. |
| [@base-ui/react](https://registry.npmjs.org/%40base-ui%2Freact) | ^1.8.0 | 1.8.0 | 1.8.0 | 1.8.0 | Already current. |
| [@hey-api/openapi-ts](https://registry.npmjs.org/%40hey-api%2Fopenapi-ts) | ^0.99.0 | 0.99.0 | 0.99.0 | Pin 0.0.0-next-20260824173136 for TS7 migration | Prerelease candidate; validate both generators and consumers. |
| [@oxlint/plugins](https://registry.npmjs.org/%40oxlint%2Fplugins) | ^1.76.0 | 1.76.0 | 1.81.0 | 1.81.0 | Update with oxlint and run custom rules. |
| [@playwright/test](https://registry.npmjs.org/%40playwright%2Ftest) | ^1.61.1 | 1.61.1 | 1.63.0 | 1.63.0 | Update together and refresh matching browser binaries/images. |
| [@storybook/addon-vitest](https://registry.npmjs.org/%40storybook%2Faddon-vitest) | ^10.5.5 | 10.5.5 | 10.6.0 | 10.6.0 | Update Storybook family together; keep Vitest 4. |
| [@storybook/react-vite](https://registry.npmjs.org/%40storybook%2Freact-vite) | ^10.5.5 | 10.5.5 | 10.6.0 | 10.6.0 | Update Storybook family together; keep Vitest 4. |
| [@tailwindcss/vite](https://registry.npmjs.org/%40tailwindcss%2Fvite) | ^4.3.3 | 4.3.3 | 4.3.3 | 4.3.3 | Already current. |
| [@tanstack/react-form](https://registry.npmjs.org/%40tanstack%2Freact-form) | ^1.33.2 | 1.33.2 | 1.33.5 | 1.33.5 | Update coordinated catalogs/consumer pins; test SSR, routing, forms and queries. |
| [@tanstack/react-query](https://registry.npmjs.org/%40tanstack%2Freact-query) | ^5.101.2 | 5.101.2 | 5.102.8 | 5.102.8 | Update coordinated catalogs/consumer pins; test SSR, routing, forms and queries. |
| [@tanstack/react-router](https://registry.npmjs.org/%40tanstack%2Freact-router) | 1.170.18, ^1.170.18 | 1.170.18 | 1.170.33 | 1.170.33 | Update coordinated catalogs/consumer pins; test SSR, routing, forms and queries. |
| [@tanstack/react-router-ssr-query](https://registry.npmjs.org/%40tanstack%2Freact-router-ssr-query) | 1.167.1 | 1.167.1 | 1.167.2 | 1.167.2 | Update coordinated catalogs/consumer pins; test SSR, routing, forms and queries. |
| [@tanstack/react-start](https://registry.npmjs.org/%40tanstack%2Freact-start) | 1.168.32 | 1.168.32 | 1.168.50 | 1.168.50 | Update coordinated catalogs/consumer pins; test SSR, routing, forms and queries. |
| [@types/node](https://registry.npmjs.org/%40types%2Fnode) | ^24.0.0 | 24.13.3 | 26.4.1 | 24.13.3 | Already latest 24.x; keep aligned with Node 24, rather than latest 26.x. |
| [@types/react](https://registry.npmjs.org/%40types%2Freact) | ^19.2.17 | 19.2.17 | 19.2.18 | 19.2.18 | Update within current major; verify affected builds/tests. |
| [@types/react-dom](https://registry.npmjs.org/%40types%2Freact-dom) | ^19.2.3 | 19.2.3 | 19.2.7 | 19.2.7 | Update within current major; verify affected builds/tests. |
| [@vitejs/plugin-react](https://registry.npmjs.org/%40vitejs%2Fplugin-react) | ^6.0.3 | 6.0.3 | 6.1.1 | 6.1.1 | Update within current major; verify affected builds/tests. |
| [@vitest/browser-playwright](https://registry.npmjs.org/%40vitest%2Fbrowser-playwright) | ^4.1.10 | 4.1.10 | 5.0.0 | 4.1.11 | Keep exact version aligned with Vitest; defer 5.0.0. |
| [class-variance-authority](https://registry.npmjs.org/class-variance-authority) | ^0.7.1 | 0.7.1 | 0.7.1 | 0.7.1 | Already current. |
| [cookie-es](https://registry.npmjs.org/cookie-es) | ^3.1.1 | 3.1.1 | 3.1.1 | 3.1.1 | Already current. |
| [husky](https://registry.npmjs.org/husky) | ^9.1.7 | 9.1.7 | 9.1.7 | 9.1.7 | Already current. |
| [iron-webcrypto](https://registry.npmjs.org/iron-webcrypto) | ^1.2.1 | 1.2.1 | 2.0.0 | 2.0.0 | Major migration; verify seal/unseal API and existing-cookie compatibility. |
| [jose](https://registry.npmjs.org/jose) | ^6.2.3 | 6.2.3 | 6.2.12 | 6.2.12 | Update within current major; verify affected builds/tests. |
| [js-yaml](https://registry.npmjs.org/js-yaml) | ^4.3.0 | — | 5.4.1 | 4.3.2 | Refresh within existing ^4.3.0 override; do not force transitive consumers to 5.x. |
| [knip](https://registry.npmjs.org/knip) | ^6.31.0 | 6.31.0 | 6.34.0 | 6.34.0 | Update within current major; verify affected builds/tests. |
| [lint-staged](https://registry.npmjs.org/lint-staged) | ^17.0.8 | 17.0.8 | 17.5.0 | 17.5.0 | Update within current major; verify affected builds/tests. |
| [lucide-react](https://registry.npmjs.org/lucide-react) | ^1.27.0 | 1.27.0 | 1.41.0 | 1.41.0 | Update within current major; verify affected builds/tests. |
| [nitro](https://registry.npmjs.org/nitro) | 3.0.260610-beta | 3.0.260610-beta | 3.0.260903-beta | 3.0.260903-beta | Still prerelease. Test SSR/container builds in separate change; stable 3.0.0 metadata is deprecated. |
| [openid-client](https://registry.npmjs.org/openid-client) | ^6.1.7 | 6.8.4 | 6.8.8 | 6.8.8 | Update within current major; verify affected builds/tests. |
| [oxfmt](https://registry.npmjs.org/oxfmt) | ^0.59.0 | 0.59.0 | 0.66.0 | 0.66.0 | 0.x minor can change behavior; inspect formatting diff. |
| [oxlint](https://registry.npmjs.org/oxlint) | ^1.74.0 | 1.74.0 | 1.81.0 | 1.81.0 | Update with @oxlint/plugins and run custom rules. |
| [playwright](https://registry.npmjs.org/playwright) | ^1.61.1 | 1.61.1 | 1.63.0 | 1.63.0 | Update together and refresh matching browser binaries/images. |
| [pnpm](https://registry.npmjs.org/pnpm) | 11.24.0 | — | 12.3.4 | 11.25.0 | Routine 11.x update; evaluate 12.3.4 separately with install/CI/container checks. |
| [publint](https://registry.npmjs.org/publint) | ^0.3.22 | 0.3.22 | 0.3.24 | 0.3.24 | Patch; rerun package export validation. |
| [qrcode.react](https://registry.npmjs.org/qrcode.react) | ^4.2.0 | 4.2.0 | 4.2.0 | 4.2.0 | Already current. |
| [react](https://registry.npmjs.org/react) | ^19.0.0, ^19.2.7 | 19.2.7 | 19.2.8 | 19.2.8 | Update React and React DOM together. |
| [react-dom](https://registry.npmjs.org/react-dom) | ^19.2.7 | 19.2.7 | 19.2.8 | 19.2.8 | Update React and React DOM together. |
| [redis](https://registry.npmjs.org/redis) | ^4.7.0 | 4.7.1 | 6.2.1 | 4.7.1 | Already latest 4.x; 6.2.1 requires SDK peer/API and session-store migration. |
| [sherif](https://registry.npmjs.org/sherif) | ^1.13.0 | 1.13.0 | 1.13.0 | 1.13.0 | Already current. |
| [sonner](https://registry.npmjs.org/sonner) | ^2.0.8 | 2.0.8 | 2.0.8 | 2.0.8 | Already current. |
| [storybook](https://registry.npmjs.org/storybook) | ^10.5.5 | 10.5.5 | 10.6.0 | 10.6.0 | Update Storybook family together; keep Vitest 4. |
| [tailwind-merge](https://registry.npmjs.org/tailwind-merge) | ^3.6.0 | 3.6.0 | 3.6.0 | 3.6.0 | Already current. |
| [tailwindcss](https://registry.npmjs.org/tailwindcss) | ^4.3.3 | 4.3.3 | 4.3.3 | 4.3.3 | Already current. |
| [tsx](https://registry.npmjs.org/tsx) | ^4.19.0 | 4.23.1 | 4.23.13 | 4.23.13 | Update within current major; verify affected builds/tests. |
| [turbo](https://registry.npmjs.org/turbo) | ^2.10.8 | 2.10.8 | 2.10.12 | 2.10.12 | Update within current major; verify affected builds/tests. |
| [typescript](https://registry.npmjs.org/typescript) | 6.0.3, 7.0.2, ^5.6.0 | 6.0.3, 7.0.2 | 7.0.2 | 7.0.2 / 6.0.3 | Migrate generator packages to TS7 with the pinned Hey API next candidate after validation; otherwise retain TS6. |
| [vite](https://registry.npmjs.org/vite) | ^8.1.4 | 8.1.4 | 8.2.2 | 8.2.2 | Update within current major; verify affected builds/tests. |
| [vitest](https://registry.npmjs.org/vitest) | ^4.1.10 | 4.1.10 | 5.0.0 | 4.1.11 | Patch now; 5.0.0 blocked by Storybook addon peer range. |
| [vitest-browser-react](https://registry.npmjs.org/vitest-browser-react) | ^2.2.0 | 2.2.0 | 2.3.0 | 2.3.0 | Update within current major; verify affected builds/tests. |
| [zod](https://registry.npmjs.org/zod) | ^4.4.3 | 4.4.3 | 4.5.4 | 4.5.4 | Update within current major; verify affected builds/tests. |
| [zustand](https://registry.npmjs.org/zustand) | ^5.0.8 | 5.0.14 | 5.0.15 | 5.0.15 | Update within current major; verify affected builds/tests. |

## Keeping updates current

The current `.github/dependabot.yml` configures NuGet and GitHub Actions only. Add npm updates for the root pnpm workspace and the standalone `/scripts/fork-smoke` directory. Group React, TanStack, Storybook, Vitest/browser adapters, and Playwright updates; route major and prerelease upgrades for manual migration review. Confirm the automation handles the pnpm catalog and chosen pnpm lockfile version before relying on it.

For NuGet, review existing group patterns against actual package names. Central package families use names such as `Microsoft.*`, `Aspire.*`, `WolverineFx*`, and `OpenTelemetry.*`; the current `microsoft-*` pattern does not match `Microsoft.Extensions.*`. Group based on actual IDs and compatibility families, with SDK pins included in the maintenance checklist.

## Validation for implementation

For the security lockfile refresh, rerun `pnpm audit --json` and affected builds. For JS dependency batches, run the existing `pnpm check` gate and app end-to-end workflows; include Storybook browser tests, generated SDK drift, and fork-smoke. Session crypto and Redis changes need existing session, login/logout, cookie, and persistence coverage. For .NET batches, run restore/build and existing unit, integration, cross-tenant, OpenAPI drift, and end-to-end checks. Exercise Aspire AppHost startup separately because a normal solution build does not prove resource orchestration.

This review queried package versions and ran the JS vulnerability audit. It did not install candidate versions, run upgrade tests, or scan deployed container digests. Recommendations are upgrade candidates, not a claim that a combined upgrade is compatible.


## Container and CI supplement

Checked upstream software releases for pinned infrastructure and the latest release majors for 20 unique external GitHub Action repositories. All action major references are current, including the exact Trivy v0.36.0 pin. This checks source selectors, not running versions or container registry digests.

| Component | Source selector | Available release | Recommendation |
| --- | --- | --- | --- |
| Mailpit | v1.22 | [v1.31.1](https://github.com/axllent/mailpit/releases/tag/v1.31.1) | Update development and test images together |
| ClamAV | 1.5.2 | [1.5.4](https://github.com/Cisco-Talos/clamav/releases/tag/clamav-1.5.4) | Patch update |
| Garage | 2.2.0 | [2.4.0](https://git.deuxfleurs.fr/Deuxfleurs/garage/releases/tag/v2.4.0) | Review storage migration notes and test; release is new and notes a Consul discovery issue |
| otel-lgtm | 0.8.1 | [0.32.1](https://github.com/grafana/docker-otel-lgtm/releases/tag/v0.32.1) | Separate observability configuration upgrade |
| Valkey | 8 in dev/test, 8.1 in production | [8.1.10](https://github.com/valkey-io/valkey/releases/tag/8.1.10), [9.1.2](https://github.com/valkey-io/valkey/releases/tag/9.1.2) | Align 8.1 across environments first; evaluate major 9 separately |
| Alpine Garage base | 3.21 | [3.24.1](https://www.alpinelinux.org/releases/) | Move to 3.24; 3.21 support ends November 1, 2026 |

Docs uses Node 22 while app images use Node 24. Standardizing docs on 24 reduces runtime variation; Node 22 is still supported. Several other images float on latest or major tags, including Alloy, Newt, nginx, Caddy, and the remote cache server. Their actual deployed versions require inspecting pulled digests; no conclusion about their patch currency follows from a floating tag.
