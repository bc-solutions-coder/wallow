**status: completed**

# Endpoint and SDK documentation

Make every published HTTP operation understandable from OpenAPI and the generated SDK without reading its implementation. Baseline: `ac6c2329245e4422a845c93d6d10df7ebb869b8f`.

The baseline publishes 164 operations, with 90 missing summaries and 161 missing descriptions. Preserve routes, operation identifiers, request/response contracts, and runtime behavior.

## Documentation standard

Each operation gets a short action summary and concise remarks that explain its caller context, important input semantics, result, and relevant side effects or errors. Read the controller and owning service/handler before making claims. Describe permission requirements and tenant scope precisely. Document redirect/cookie workflows as browser flows, and direct consumers toward SDK client factories where those factories own authentication.

Source XML documentation owns OpenAPI and generated function documentation. Never hand-edit generated files. Public hand-written SDK functions get useful JSDoc where missing or misleading. Improve the existing SDK integration reference rather than creating a competing onboarding guide.

## Work

1. Inventory operations and public SDK functions. Record deliberate exclusions such as OIDC protocol, local signed storage transport, operational endpoints, and test-only routes.
2. Independently document Identity organization/member/client administration; Identity authentication/configuration; and remaining module operations in disjoint controller files.
3. Review SDK entry functions and improve the existing integration reference with concise operation discovery, client selection, calling, and error-handling examples.
4. Regenerate OpenAPI and SDK. Verify every published operation has substantive summary/description documentation and generated function documentation.
5. Verify executable C# and TypeScript tokens and OpenAPI contract data remain unchanged. Run formatter, solution build, relevant backend checks, pnpm check, and DocFX. Inspect published declaration comments and generated reference output.
6. Review changes, mark this plan completed, commit and push the current branch, and verify CI.

The parallel work follows the dispatching-parallel-agents skill. Workers edit only their assigned controller files. The lead owns SDK files, shared OpenAPI metadata, guides, inventory/verification, generation, and git operations.

## Result and verification

All 164 published operations have an action summary and behavior description. Regeneration carries them into all 164 endpoint functions and 231 TanStack query/mutation helpers. The latter use supported generator hooks so query keys also explain their purpose. Generation rejects operations without summaries or descriptions and exported query helpers without mapped documentation. Reviewed all 52 public hand-written SDK functions/classes; revised misleading or historical prose and documented public handler/store methods.

The existing integration guide now covers six entrypoints, choosing a client, finding operations, passing path/query/body options, direct return values, errors, and query/mutation usage. Its three new or corrected examples passed strict typechecking against the built declarations with library checks enabled.

OIDC protocol routes, signed local-storage transport, operational routes, and test-only routes remain outside the published operation set. Their omission is deliberate; browser authentication workflows are covered by the integration guide and SDK helpers.

Validation completed:

- OpenAPI coverage: 164/164 summaries and descriptions. All 379 OpenAPI differences are documentation fields; server URL, routes, operation IDs, and contract data are unchanged.
- Roslyn comparison: all 28 changed C# files preserve executable tokens, literals, directives, disabled text, and protected comments.
- TypeScript AST comparison: all 27 changed SDK source/generated files preserve executable tokens. Generator configuration and its new documentation hook are the intentional generation-only code changes.
- Generated source, built declarations, and packed SDK declarations: all 395 public generated functions retain both endpoint summary and description.
- `dotnet format api/Wallow.slnx` and solution build passed with zero build warnings/errors.
- `./scripts/run-tests.sh`: 5,172 fast tests passed. Integration tests were not run for this documentation-only API change.
- `pnpm check` passed, including regeneration drift, builds, typechecks, tests, export checks, and the external package consumer.
- DocFX build passed; inspected rendered integration and controller documentation. Three pre-existing warnings remain: a newer Razor analyzer and two inherited EF method references in `TenantSaveChangesInterceptor`.
- Independent Standards and Spec reviews found no actionable issues.

Per-operation claims were checked against owning controllers and services. Descriptions reflect existing limitations, including global user search with tenant-scoped roles, non-persistent organization logo/branding fields, account-wide sessions, and the current access checks on MFA administration. No runtime behavior was changed.
