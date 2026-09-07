**status: completed**

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

## Browser acceptance completed

Verified on 2026-09-07 using the user's Google Chrome on macOS, against local Wallow commit `3d36327b` started through Aspire. A temporary localhost consumer used the seeded administrator's organization session through the existing BFF; tokens and private signing keys stayed server-side.

- Wallow generated the organization signing key and registered a real Chrome PushManager subscription.
- Actual encrypted push delivery reached the service worker, which completed `showNotification`. The user confirmed the notification appeared in macOS Notification Center, then confirmed normal delivery after adjusting notification settings.
- Clicking the notification raised `notificationclick` and opened the expected same-origin `/verify-221.html?clicked=true` destination, observed in Chrome.
- Rotation preserved delivery to the existing subscription using its retained signing version.
- Unsubscribe removed the Wallow registration and browser subscription; resubscription succeeded using the new signing version.
- After all three consumer tabs were closed, a delayed Wallow send returned 204 and the service worker received the real push at 05:22:37 UTC. Clicking that background notification reopened the expected destination.

Receipt and click events were recorded by the temporary service worker during the run, alongside UI observations. No DevTools-generated or mocked push event was used. Initial missing banners were an OS notification presentation setting, not a delivery failure. This verifies Chrome on this Mac, not every browser/OS combination. Temporary test tooling remains outside the repository; no application code changed during verification.
