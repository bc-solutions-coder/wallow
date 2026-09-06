**status: completed**

# Security analysis without paid private-repository security features

Research for [Assess license-free security analysis for private forks](https://github.com/bc-solutions-coder/wallow/issues/224). This report supplies evidence for a later human decision; it selects no profile and changes no workflows. “Unlicensed” means no paid security subscription, not exemption from tool licenses. Sources inspected 2026-09-06; repository baseline `5c6aada6`.

## Findings that affect the decision

A private fork can run local security analysis without a scanner service credential. Existing .NET analyzers plus DevSkim and offline zizmor form a feasible candidate, with an explicit loss of CodeQL's database and framework-based analysis. Semgrep CE is also usable locally, but its documented C# support is substantially below this repository's language level. It should not be advertised as equivalent C# coverage.

CodeQL's CLI is not an entitlement workaround: GitHub documents public-repository use and eligible organization repositories with GitHub Code Security enabled. Its database queries and framework models are the comparison baseline. GitHub's code-scanning UI also has repository eligibility requirements, independently of whether a third-party tool emits SARIF. The private profile must therefore gate locally and retain reports as ordinary private Actions artifacts, without depending on SARIF upload. [CodeQL CLI](https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-cli), [CodeQL code scanning](https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-code-scanning).

## What Wallow already checks

At the inspected commit, `api/Directory.Build.props` enables `AnalysisMode=All`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest` and warnings-as-errors. `api/Directory.Build.targets` attaches NetAnalyzers, StyleCop, Meziantou and Roslynator; NetAnalyzers is pinned to `10.0.302`. The editorconfig overrides individual diagnostic severities, so package presence does not prove every possible diagnostic blocks merging. Microsoft's security catalog includes injection, deserialization, XML, cryptography and certificate-validation checks. This is already meaningful security coverage, not just formatting. A future implementation should record effective security rule settings and preserve them in the ordinary build. [Microsoft security rule catalog](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/security-warnings).

The current CodeQL matrix scans C# and Actions, not JavaScript/TypeScript. Its manual C# build disables ordinary analyzers because the normal CI build owns those checks. Actionlint is separately pinned to `1.7.12`. It validates workflow syntax, expressions, references and embedded scripts through optional shellcheck/pyflakes; its documented checks include untrusted expression injection. Zizmor adds dedicated workflow security policy coverage rather than replacing actionlint. [Actionlint checks](https://github.com/rhysd/actionlint/blob/main/docs/checks.md). These local facts can be reproduced from the files above and `.github/workflows/{codeql,actionlint}.yml` at the baseline commit.

## Candidate comparison

| Candidate | Eligibility and actual coverage | Runner and gate |
| --- | --- | --- |
| DevSkim | MIT-licensed engine and bundled rules; no paid tier required for these rules. C# supported through text patterns and configuration queries, not a compiler dataflow engine. Rules cover weak hashes, TLS and dangerous API/configuration patterns. | .NET global tool or self-contained binaries on Linux/macOS/Windows. Local SARIF; severity/confidence filters. Requires an explicit findings gate: default success exit status does not mean no findings. |
| Semgrep CE | LGPL-2.1 engine; community rules have a separate restrictive license. Official CE table documents C# only through 7.0, community support and single-function analysis. Pro's modern C# and cross-file claims do not apply. | Python package or container, no login for local scan. Pin engine and approved rules; JSON/SARIF and `--error`/severity options. Parsing failures and skipped inputs must be treated as coverage failures. |
| zizmor | MIT, dedicated GitHub Actions security audits; no C# analysis. Offline audits include dangerous triggers, injection and permission/secrets patterns. | Binary or Python-distributed binary; local checkout with `--offline`, no GitHub API credentials. JSON/SARIF, severity/confidence filters and nonzero finding exits. |

Sources: [DevSkim README](https://github.com/microsoft/DevSkim), [DevSkim license](https://github.com/microsoft/DevSkim/blob/a452aa506f80928b611c4c7bfe306b8115821aae/LICENSE.txt), [DevSkim CLI options](https://github.com/microsoft/DevSkim/blob/a452aa506f80928b611c4c7bfe306b8115821aae/DevSkim-DotNet/Microsoft.DevSkim.CLI/Options/BaseAnalyzeCommandOptions.cs), [Semgrep CE language table](https://docs.semgrep.dev/semgrep-ce-languages), [Semgrep CLI](https://docs.semgrep.dev/cli-reference), [zizmor license](https://github.com/zizmorcore/zizmor/blob/main/LICENSE), [zizmor quickstart](https://docs.zizmor.sh/quickstart/).

### Coverage and maintenance details

DevSkim's current upstream source targets .NET 8/9/10. Its maintained rule tree is inspectable, including `security/cryptography/hash_algorithm.json`; these are pattern checks, not evidence that it follows hostile request data through Wallow modules. Pin a released CLI (bundled rules), review rule changes on upgrades and track suppressions with reasons. The inspected main commit was `a452aa506f80928b611c4c7bfe306b8115821aae`; the installed stable release was older, so do not assume all main-branch rules ship in it. [Rule tree](https://github.com/microsoft/DevSkim/tree/a452aa506f80928b611c4c7bfe306b8115821aae/rules/default), [CLI project](https://github.com/microsoft/DevSkim/blob/a452aa506f80928b611c4c7bfe306b8115821aae/DevSkim-DotNet/Microsoft.DevSkim.CLI/Microsoft.DevSkim.CLI.csproj), [changelog](https://github.com/microsoft/DevSkim/blob/main/Changelog.md).

The public Semgrep rule repository contained 52 C#-tree YAML files at `40b8c63f75dc7c22c8a77482d73bfb864b146f7e`: examples include SQL/OS-command injection, SSRF, deserialization and legacy ASP.NET configuration. This counts files, not distinct enabled rules or effective detection coverage. Many patterns target older APIs; presence is not proof of suitability for .NET 10 or Wallow's framework usage. Updates require both engine compatibility and rule-pack review. [Inspected C# tree](https://github.com/semgrep/semgrep-rules/tree/40b8c63f75dc7c22c8a77482d73bfb864b146f7e/csharp).

Semgrep's rules license permits internal business use but disallows distribution and provision as a service. Thus an internal fork scan is within the stated purpose, but copying that rule pack into a redistributable fork template is not an appropriate default. Keep rule acquisition separate, preserve notices, or use separately licensed original rules. Those original rules incur authorship and maintenance costs. This limitation concerns scanner rules, not a blanket prohibition on scanning software that happens to be a SaaS product. [Rules license](https://semgrep.dev/legal/rules-license/).

Zizmor offline deliberately omits audits needing GitHub API access. Document that loss; do not claim remote action provenance/archive checks ran. Pin its version, audit configuration and persona because these affect enabled findings. [Operating modes](https://docs.zizmor.sh/usage/), [audit catalog](https://docs.zizmor.sh/audits/).

## Disposable execution evidence

On the local macOS host, installed `zizmor 1.30.0` through `uv tool run` and DevSkim `1.0.90` in `/tmp/devskim-cli-224`. No scanner API token was supplied. Fixtures and reports stayed under `/tmp`; no repository tests were added.

- A workflow with `pull_request_target` and direct interpolation of a PR title in `run` produced `zizmor/dangerous-triggers` and `zizmor/template-injection` SARIF errors. A JSON scan with `--offline --min-severity high` exited 14. Commands used explicit offline mode.
- A C# method returning `MD5.Create()` produced DevSkim `DS126858` with SARIF level `error`. The default scan nevertheless exited successfully. Its `-E` option returns a finding count; a robust gate should parse validated SARIF after checking scanner execution success, rather than rely on a count that can wrap in a process exit status.

These are positive smoke checks, not a benchmark, whole-repository scan or Linux-runner verification. Semgrep was not executed; its language limitation above is from its official CE documentation. Ordinary .NET builds, effective analyzer configuration, false-positive rate, current-project parser coverage and cross-method vulnerability fixtures were not evaluated.

## Boundaries for the follow-up decision

The evidence supports offering a free local profile, but not claiming CodeQL parity. Accepting it means acknowledging reduced framework/dataflow coverage and absent GitHub code-scanning UI where ineligible. Preserve dependency and image vulnerability scanning separately; these source analyzers do not replace either requirement. Tool installation may use public network downloads; “no private credentials” is distinct from “air-gapped.” Checkout still needs the normal narrowly scoped repository token for a private Actions job.

Before implementation, choose enabled rules/severity, handling of suppressions and incomplete scans, and whether Semgrep's documented language ceiling makes it unsuitable for the baseline. A selected profile should fail on missing reports/tool failures, publish a coverage summary, and pin tool/rule identities. This report makes none of those product decisions.
