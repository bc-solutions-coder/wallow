# CI credential and publication safeguards

Research for [Establish GitHub Actions credential and publication safeguards](https://github.com/bc-solutions-coder/wallow/issues/207). Observed 2026-09-06 against repository commit `21a0b5fc`. This establishes constraints and options; it does not select the maintainer's trust policy or change workflows/settings.

## Findings that constrain the design

The current boundary is **fork versus same-repository code**, not reviewed versus unreviewed code. The JS and route-tree PR jobs make Turbo credentials available at job scope and join the tailnet when OAuth credentials exist. Same-repository PR code can therefore run before review with those capabilities. Dependency install, build tools and tests are all executable code. Moving a secret to a later step reduces accidental exposure but cannot undo an earlier compromise of that runner. Separate jobs with separate permissions and explicit artifact validation are the stronger boundary. This is a risk inference from the workflow evidence below, not evidence of compromise.

The live repository settings also do not establish “main passed CI” as a guaranteed fact. Main currently forbids deletion and force pushes, but has no observed required review or check rules. Publication on main/tag events must not be treated as a validation gate without an explicit dependency or enforced ref policy.

## Repository evidence

All file links below point to the inspected source snapshot. Live API results are a point-in-time observation, not immutable settings history.

| Surface | Observed behavior | Design implication |
| --- | --- | --- |
| [JS CI](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/js.yml), [route drift](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/route-tree-drift.yml) | `pull_request` and main push; read-only contents token; job-level `TURBO_*`; conditional Tailscale OAuth action | Fork absence of secrets is useful; same-repository PRs remain a credential/network trust decision. |
| [Package Publish](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/package-publish.yml) | Package tag or manual dispatch; whole job has `packages: write`; installs, builds, tests and publishes on one runner; registry token passed explicitly to registry steps; no environment | Earlier code runs in the same job as publication. Dispatch does not visibly constrain selected ref to main; tag naming alone does not prove reviewed ancestry. |
| [OpenAPI Auto-Regen](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/openapi-autoregen.yml) | Main push or dispatch; checkout uses release PAT with GITHUB_TOKEN fallback; then executes .NET and JS; contents/PR write | Long-lived automation credential is persisted before repository code runs. Its exact scopes are unknown. |
| [Release Please](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/release-please.yml) | Main push; release PAT supplied to third-party action | Separate automation from arbitrary build code; replace/restrict PAT only with a deliberate event-chain design. |
| [Image publication](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/publish.yml) | Promotes mutable seven-character SHA tags; vulnerability scan occurs **after** promotion | A failed scan does not prevent already-published tags; existence of an image is not proof of its checks/provenance. |
| [Deployment](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/deploy.yml), [docs](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.github/workflows/docs.yml) | Main builds/pushes images; docs has a separate Pages environment job | Publishing credentials are job-scoped in these workflows, but publishing is not opt-in for forks through an explicit feature setting. |
| [Workflow directory](https://github.com/bc-solutions-coder/wallow/tree/21a0b5fc/.github/workflows) | Remote actions use version tags; no executable `pull_request_target` or `workflow_run` trigger found; no `persist-credentials: false` found | Current design avoids the classic privileged fork-checkout chain, but action version tags remain mutable. |
| [Workspace](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/pnpm-workspace.yaml), [root manifest](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/package.json), [.npmrc](https://github.com/bc-solutions-coder/wallow/blob/21a0b5fc/.npmrc) | pnpm 11.24.0 pinned; frozen installs; `allowBuilds.esbuild: false`; root `prepare` invokes Husky; workspace dependencies require no registry token | Good reproducibility and dependency-script controls already exist. They do not sandbox build/test execution or workspace scripts. No explicit release-age/trust policy is declared. |

Read-only API evidence collected using `gh api repos/bc-solutions-coder/wallow/<path>`:

| Endpoint suffix | Returned facts |
| --- | --- |
| `actions/permissions` | enabled; `allowed_actions: all`; `sha_pinning_required: false` |
| `actions/permissions/workflow` | `default_workflow_permissions: write`; `can_approve_pull_request_reviews: true` |
| `actions/permissions/fork-pr-contributor-approval` | `approval_policy: first_time_contributors` |
| `rulesets`, `rulesets/14131111` | One active repository ruleset for main; only deletion and non-fast-forward restrictions; no bypass actors |
| `branches/main/protection` | HTTP 404, “Branch not protected” (classic protection endpoint; does not negate the ruleset above) |
| `environments` | Only `github-pages`; branch-policy protection; administrators can bypass; no required-reviewer rule returned |
| `environments/github-pages/deployment-branch-policies` | Allows branch `main` |

## Documented platform constraints

- Ordinary fork PRs receive no repository secrets and a read-only GITHUB_TOKEN; Dependabot PRs receive similar treatment. Enabling Actions in a newly forked repository is still necessary. `workflow_run` can receive write tokens/secrets even when its initiating workflow could not, so an artifact or cache crossing that boundary requires validation. [GitHub event reference](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)
- Same-repository PRs are not covered by the fork-secret restriction. Repository secrets must consequently be treated as available to same-repository workflow code unless stronger configuration gates them. [GitHub secrets reference](https://docs.github.com/en/actions/reference/security/secrets)
- Specify minimal `permissions` for each job; unspecified permissions become `none` once permissions are explicitly enumerated. Workflow syntax controls GITHUB_TOKEN, not the privileges of a separately supplied PAT. [Workflow permissions](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#permissions)
- Actions can access `github.token` even without receiving it as an input. GITHUB_TOKEN-created events generally do not trigger another workflow, except dispatch events. A GitHub App installation token or PAT is an option where fresh events are necessary; model this explicitly rather than quietly relying on fallback behavior. [Token authentication](https://docs.github.com/en/actions/tutorials/authenticate-with-github_token)
- Checkout defaults to persisting credentials. v6+ stores them under RUNNER_TEMP; relocation is not isolation from later runner processes. Use `persist-credentials: false` for jobs that do not push. v7 also refuses unsafe fork checkouts under privileged triggers by default, but does not make arbitrary untrusted code safe. [Checkout documentation](https://github.com/actions/checkout)
- Full commit SHAs make action references immutable; tags do not. Review selected action source and transitive behavior; pinning does not establish that an initially selected commit is trustworthy. GitHub recommends avoiding untrusted execution under privileged triggers and minimizing third-party action exposure. [Secure use reference](https://docs.github.com/en/actions/reference/security/secure-use)
- pnpm's `allowBuilds` permits/denies dependency lifecycle scripts; unknown builds fail under `strictDepBuilds`. `ignoreScripts` also suppresses project scripts but does not suppress `.pnpmfile.mjs`. Neither prevents explicitly invoked tools or imported dependencies executing later. These controls narrow installation exposure; they are not a runner sandbox. Current docs default to v12, so retain the repo's v11 pin and verify options against that version before implementation. [pnpm build settings](https://pnpm.io/settings/build)
- Release-age delays, trust policy and exotic-subdependency restrictions offer additional dependency-resolution controls. These govern selection/trust characteristics rather than proving absence of malicious runtime behavior; exact v11.24.0 behavior should be tested before adopting a configuration copied from current v12 docs. [pnpm dependency settings](https://pnpm.io/settings/dependency-resolution)
- This repo publishes JavaScript to **GitHub Packages**, not npmjs.org. GitHub Packages supports the repository GITHUB_TOKEN for Actions publishing; cross-repository installs require package access grants or appropriate credentials. Fork publishing requires matching namespace, package metadata and grants, not merely replacing a secret. [GitHub npm registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-npm-registry)
- npm trusted publishing uses OIDC and constrained publisher identities for npmjs.org. It is a useful option if that registry is chosen, not a replacement authentication mode for GitHub Packages. [npm trusted publishers](https://docs.npmjs.com/trusted-publishers/)

## Options for the decision ticket

1. Make all PR validation secret-free, including same-repository branches. Reserve private cache/network and write-capable jobs for explicitly trusted refs. This gives the most uniform fork behavior but may give up private remote-cache acceleration before merge.
2. Permit same-repository PR cache access deliberately, with narrowly scoped network grants and separate cache read/write identities. This accepts pre-review code execution with those capabilities; “member branch” is an authorization policy, not proof that dependencies are safe.
3. Separate artifact construction from package/container publication. A minimal publisher receives an exact artifact/digest and provenance from a validated trusted run, and does not execute arbitrary project lifecycle scripts. Required checks, protected tags/refs, protected environments and dispatch ref validation enforce different parts of that chain.
4. Keep release automation as a small write-capable workflow, preferably with a narrowly scoped short-lived App token when event chaining requires it. Keep its credential off build runners. Use explicit opt-in settings for fork publishing and document namespace/package access configuration.

These options can be combined. The human must decide which code is trusted with private cache/network access, who can release, whether approvals are needed, and which checks must precede publication.

## Limits

No secret values were read, no executable dependency was installed, and no proof-of-exploit was attempted. PAT scopes/expiry, Tailscale ACLs/OAuth grants, cache server authorization, registry package grants, maintainer account security and organization policies were not established. No exhaustive runner history or repository incident investigation was performed. Workflow and settings inspection establishes exposure paths, not token theft or compromise. No CI design can promise zero supply-chain risk; reducing capabilities and enforcing artifact/ref provenance constrains consequences.
