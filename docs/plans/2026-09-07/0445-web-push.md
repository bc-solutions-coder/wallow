**status: active**

# Complete browser Web Push (#221)

Authoritative scope: approved agent brief on GitHub issue #221. Review baseline: cba53c53.

1. Introduce typed browser subscriptions and encrypted organization signing-key versions, with public discovery and retirement validation.
2. Implement encrypted authenticated provider requests, protected outbound transport, failure categories and race-safe subscription lifecycle.
3. Update SDK/OpenAPI and consumer service-worker integration documentation/example.
4. Verify approved seams: behavioral registration/ownership HTTP tests; independent protocol encryption/authentication tests; real-browser receipt, notification and click with a provisioned subscription.
5. Run repository quality gates and parallel standards/spec code review, resolve findings, commit and push current branch.

No source-text tests. Preserve #222 safeguards. Actual push-service acceptance is not browser delivery. Report external verification blockers explicitly.

## Implementation and verification

Source implementation, SDK/OpenAPI regeneration, consumer guide and independent standards/spec reviews are complete. Both review axes have no remaining code findings.

- Notifications module: 645 passed, with three existing skips reported by the runner.
- Protocol/provider: 53 native tests passed, including independent decryption/signature verification and actual HTTP redirect refusal.
- Web Push HTTP integration: 13 passed with real OAuth2 authorization, key lifecycle and PostgreSQL interleaving checks. Existing ownership coverage also passed.
- Architecture/codegen/ownership integration check: 371 architecture, 2 codegen and 20 HTTP tests passed before the final concurrency cases were added.
- Full backend run: 5,596 passed and 2 failed. One failure was a temporary documentation-compilation file disappearing during an existing repository sweep; the other was an explicit OpenAPI400 annotation overriding the standard validation schema. The temporary file was removed and the annotation corrected. Targeted rerun of both affected test classes passed all 11 tests.
- Final `pnpm check` passed, including generated artifacts, builds, typechecks, browser/unit tests, exports and external consumer verification.
- The guide's TypeScript subscription example compiled against the generated SDK. DocFX built with three existing analyzer/XML-reference warnings.

## Remaining acceptance blocker

Actual browser push receipt/display/click has NOT been verified. Chrome was launched with user permission; Chrome, the enabled extension and native-host manifest were confirmed present. The browser runtime still returned no available browsers after retry. The Chrome plugin requires reconnection/reinstallation from Codex's plugin UI before this check can run. Keep issue #221 open and this plan active; HTTP acceptance and controlled protocol tests do not substitute for browser delivery.
