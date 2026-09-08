**status: completed**

# CodeQL full scans and exact finding exceptions

The local security gate must see the same query coverage on pull requests and main. The reusable CodeQL job now sets `CODEQL_ACTION_DIFF_INFORMED_QUERIES=false` for both initialization and analysis. This retains the pinned action, local SARIF gate, security severity threshold, and artifact retention.

At pinned action commit `cdf488f595d80d6e07e03d4674febd5ab45fa938`, [feature-flags.ts](https://github.com/github/codeql-action/blob/cdf488f595d80d6e07e03d4674febd5ab45fa938/src/feature-flags.ts#L232) enables diff-informed queries by default and gives an explicit false environment setting precedence. [diff-informed-analysis-utils.ts](https://github.com/github/codeql-action/blob/cdf488f595d80d6e07e03d4674febd5ab45fa938/src/diff-informed-analysis-utils.ts#L28) exits before computing PR diff ranges when that feature is disabled. Hosted verification must confirm a PR still reports findings outside its changed lines.

CodeQL exceptions now require `primary_location_line_hash` and `message_sha256`, in addition to the exact scanner, rule, and file. The normalizer retains the SARIF `primaryLocationLineHash` and hashes the exact UTF-8 `message.text`. Missing finding metadata cannot match an exception. Requiring these selectors prevents a reviewed finding from exempting every result of the same rule in the controller, while avoiding dependence on absolute line numbers. Any changed selector requires renewed review. These are finding identifiers, not proof that application behavior remains safe; behavior tests and the existing 30-day maximum expiry remain necessary.

The combined authentication fix records one reviewed consent exception using the post-fix fingerprint and message hash, with a 30-day expiry. The finding remains visible in normalized reports. `acceptedTerms` expresses agreement; a protected typed provider state, verified email for existing-account linking, and successful linking independently guard sign-in. Other scanners retain their existing exact file/package scopes. Scanner-local suppressions remain forbidden.

Validation:

- The new behavior tests failed against the old matcher, which hid another finding with the same rule and file.
- All 24 CI helper tests pass, including altered or missing fingerprint/message, missing exception selectors, duplicate selectors, exact expiry, and the 30-day bound.
- `actionlint .github/workflows/codeql.yml` and `git diff --check` pass.
- Replaying the saved main C# SARIF produces 11 findings and one unexcepted blocker, confirming no consent suppression was introduced.
- Replaying post-auth PR287 run `34170166071` at head `57060dab70a0139360f954ac60885aa844c8592a` retains its one unexcepted blocker. Its fingerprint remains `75ca4abef0f0e3ae:1`, while its message SHA-256 is now `144caff75340103d793aef9aed833a3667cd51577dfe3454220b392a27735771`. The earlier main result had a different message hash; any future exception must use the reviewed post-fix result.

After adding the exact exception, replaying that post-auth report retains one visible finding with zero unexcepted blockers. The old main report still fails with one unexcepted blocker. Hosted full-scan verification remains pending; unrelated or changed findings must continue to block.
