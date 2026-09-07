# API comment review #233

Review of [#233](https://github.com/bc-solutions-coder/wallow/issues/233), following the resolutions in [#230](https://github.com/bc-solutions-coder/wallow/issues/230), [#231](https://github.com/bc-solutions-coder/wallow/issues/231), and [#232](https://github.com/bc-solutions-coder/wallow/issues/232).

Baseline: `258f27eabbe6676598a79097ff99cae899dc0e81`. Work was isolated from the shared checkout. The authoritative local and remote `main` still matched that baseline at reconciliation.

## Coverage

[files.tsv](files.tsv) records all 1,766 tracked API files, their baseline/final SHA-256 hashes, dispositions, evidence, and unresolved findings. [comments.tsv](comments.tsv) records all 5,313 baseline comments: 4,472 C# trivia blocks and 841 configuration, shell, and embedded Markdown comments. Locations are baseline line numbers; adjacent `//` lines have separate rows because Roslyn represents them separately. XML documentation is one block. Standalone Markdown prose is explicitly outside the code-comment rewrite.

Every comment was read with its owning code or configuration; callers, handlers, tests, and existing guides resolved claims where needed. The records include useful comments retained without cosmetic edits. The final reconciliation also corrected disposition labels for consolidated/deleted comment groups and protected directives. No unresolved findings remain.

| File disposition                           | Count |
| ------------------------------------------ | ----: |
| Reviewed and rewritten                     |   671 |
| Reviewed and already useful                |   118 |
| Verified no comments                       |   940 |
| Generated C# preserved byte-for-byte       |    22 |
| Standalone guide without embedded comments |    15 |

| Comment disposition | Count |
| ------------------- | ----: |
| Rewrite             | 2,967 |
| Keep                | 1,276 |
| Remove              |   899 |
| Protected           |   168 |
| Move                |     3 |

The three move records point to existing authoritative guides: integration-event authoring, membership-event semantics, and explicit module registration. Their longer explanations already exist there; concise source contracts remain. No new architectural guide was needed.

## Verification

- C# executable tokens and literals, disabled text, directive tokens, and protected comment text/next-token placement are unchanged. The inventory includes comments attached to preprocessor directives. Generated C# hashes are unchanged.
- All 113 non-C# API files passed format-aware comparisons: XML elements/attributes/values, strict JSON values, EditorConfig settings/sections, Bash AST/token spellings and shebang, and Markdown content outside parsed embedded comments.
- OpenAPI regenerated through the repository's build-time emission recipe. All 73 differences are at identified OpenAPI documentation fields. Paths, operations, schema properties, examples, constraints, defaults, security data, and `http://localhost:5001/` remain unchanged.
- SDK output was regenerated, not hand-edited. Its three changed TypeScript files have identical non-comment tokens. Regenerating `api-errors` produced no changes. [generated.tsv](generated.tsv) records the output hashes.
- `dotnet build api/Wallow.slnx`: passed with zero warnings and errors.
- `dotnet format api/Wallow.slnx`: passed; its diff was checked for non-comment changes.
- `./scripts/run-tests.sh`: 5,172 passed. The Notifications runner also reports three existing skipped tests; the script's aggregate omits those skips. Integration was excluded from this invocation.
- `./scripts/run-tests.sh integration`: 426 passed, zero failed or skipped.
- `pnpm check`: passed after staging regenerated output, as required by its generated-drift check. This includes formatting, linting, dependency/environment checks, builds, typechecks, tests, exports, and the external consumer.
- `dotnet tool restore` and `dotnet docfx docfx.json`: passed with the same three warnings reproduced on the unchanged baseline: the Razor analyzer requires a newer compiler, and inherited EF documentation has two unsupported overload `cref` values. No new documentation warnings.

DocFX HTML was inspected for MFA responses, organization-client registration, branding replacement rules, integration-event authoring, and storage contracts. The corrected summaries appear in generated reference pages; links and code formatting are retained. Infrastructure reference pages are excluded by the existing DocFX filter. Browser automation was unavailable, so this inspection used the generated HTML and metadata rather than a visual browser check.

The last small corrections were the MFA response's conditional ticket description a redundant expiration-check comment, and qualification of cookie reissue after new-session creation. Final token/configuration audits cover them; they introduce no executable changes. No new source-text assertion tests were added.

## Rerun the one-off audits

These are review utilities, not application tests. Run from the repository root with the baseline commit available:

```sh
dotnet run --project docs/agents/api-comment-review-233/audit/audit.csproj -- "$PWD"
python3 -m venv /tmp/wallow-comment-review-venv
/tmp/wallow-comment-review-venv/bin/pip install -r docs/agents/api-comment-review-233/audit/requirements.txt
/tmp/wallow-comment-review-venv/bin/python docs/agents/api-comment-review-233/audit/manifest.py "$PWD"
python3 docs/agents/api-comment-review-233/audit/openapi.py "$PWD"
node --input-type=commonjs - "$PWD" \
  "$PWD/node_modules/.pnpm/typescript@5.6.1-rc/node_modules/typescript" \
  < docs/agents/api-comment-review-233/audit/typescript.cjs.txt
```

The archived TypeScript audit runs through stdin so it is not treated as an unused application entry by Knip. It uses the JavaScript compiler API already installed transitively in this lockfile; the workspace's TypeScript 7 package uses the native compiler. The OpenAPI comparator identifies documentation fields by object location and never globally strips keys named `description`.

## Final review

### Standards

No documented-standard violations or actionable code smells found. The reviewer checked guidance, audit utilities, and selected contract-sensitive hunks.

### Spec

One minor wording issue was found and fixed: `EnsureSessionIdAsync` returns early for a live SID, so its summary now limits cookie replacement to creation of a new session. No other substantiated acceptance gaps or scope creep were found.

Both reviews supplement the complete per-comment reading record; they do not claim a second independent reading of every comment. Standards: zero findings. Spec: one finding, resolved.
