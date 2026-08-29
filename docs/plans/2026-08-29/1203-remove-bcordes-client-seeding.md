**status: active**

# Remove the bcordes client seeding

## Goal

Strip the `bcordes` branding and plumbing out of the seeding surface. Wallow is a fork-first
base platform; `bcordes.dev` is one deployment of it, and its clients have leaked into the base
repo's seeds, compose files, secret generator, and tests. After this plan, the base seeds only
what the platform itself needs, and a real deployment provisions its additional OIDC clients
**through the UI** — `ClientsController` already offers full CRUD plus `rotate-secret` (and a
parallel service-account surface), and wallow-web's organization detail screen already wires
`clientsCreateMutation`. Nothing new needs building; this is deletion and one rename.

## Inventory (verified 2026-08-29)

| Artifact | Where | Status |
| --- | --- | --- |
| `sa-bcordes-bff` client | `api/seed.json` clients[1] | **Dead.** No consumer anywhere — only an empty-string mapping row in `SeederClientSecretMappingTests` and historical plan docs. |
| `bcordes-bff` client | `api/seed.json` clients[2] | **Load-bearing fixture, wrongly named.** The e2e `bff-example` container authenticates as it; it is the deliberately-NOT-first-party client whose authorize round trip renders the real consent screen (`external-origin-login.spec.ts`). |
| `bcordes-web-client` | `api/seed.json` `_productionExampleClients` | Documentation-only (seeder ignores the key); pinned by `SeedJsonProductionExampleTests`. |
| `ClientSecrets__bcordes-dev-client/sa-bcordes-bff/bcordes-bff` | `docker/docker-compose.production.yml` | Optional bare-var injection slots for the bcordes deployment. Note `bcordes-dev-client` names a client **no seed file defines** — a set secret would abort the seeder (fail-closed). |
| `BCORDES_*` block | `docker/.env.production.example` (commented), `scripts/prod-secrets.sh` (comment), `docker/CLAUDE.md` ("never fix the bare `${BCORDES_*}` vars") | Support surface for the row above. |
| `accessRequestEmail: bryan.cordes@bcordes.dev` | `docker/seed.production.json` org "Wallow" | Personal address as org config. |
| Tests | `SeedJsonBffClientTests` (pins `bcordes-bff` shape), `SeedJsonProductionExampleTests` (pins the example block), `SeedClientIdConsistencyTests` (compose `ClientSecrets__*` ↔ seed clients sweep), `SeederClientSecretMappingTests` (one mapping row) | Follow their artifacts. |
| Docs | `docs/integrations/typescript-sdk.md` (§ "the seeded `bcordes-bff` client"), `docs/development/testing-e2e.md`, `docs/development/testing.md`, `docs/operations/deployment.md:231`, `apps/wallow-web/e2e/CLAUDE.md`, comments in `docker/docker-compose.test.yml` | Rename/remove alongside. |

Historical records (`docs/plans/2026-07-31/…`, `docs/agents/beads-archive/`, `CHANGELOG.md`)
are left untouched — they describe what was true when written.

## Decisions

1. **The third-origin e2e client stays seeded, but renamed.** The alternative — having the e2e
   stack create it at boot through the clients API — was rejected: seeding is the supported
   provisioning mechanism for dev/test stacks, the client must exist before `bff-example` can
   start, and runtime creation would add an ordering step for zero product value. The
   bcordes problem is the *name*, not the mechanism. Rename `bcordes-bff` →
   **`bff-example-client`** (secret `bff-example-secret`), matching the container that uses it.
2. **`sa-bcordes-bff` is deleted, not renamed.** Nothing exercises the client-credentials flow
   against it. If a seeded service-account fixture is ever needed again, the service-account
   API (`POST /v1/identity/clients/service-accounts`) is the way to make one.
3. **Production provisions extra clients through the UI, full stop.** The three
   `ClientSecrets__bcordes-*` compose slots and the `BCORDES_*` env block are deleted rather
   than genericized: the injection mechanism (`ClientSecrets__<clientId>`) remains documented
   and available for forks that *do* want to seed a client, but the base compose file stops
   shipping slots for one specific person's deployment. `ClientSecrets__wallow-web-client`
   stays — that client is the platform's own dashboard.
4. **`bcordes-web-client` and its `_productionExampleClients` key are deleted.** Its job —
   "show what a production client looks like" — moves to prose: a short deployment.md section
   pointing at the organization clients UI (create → copy secret from `rotate-secret`) instead
   of a copy-paste JSON block that invites secret-in-file mistakes.
5. **`accessRequestEmail` leaves the production seed.** Verify the field is optional in
   `OrganizationSeedSyncService`/its options binding (expected: yes); if so drop it and let the
   deployment set it through the org settings UI. If it turns out required, replace with a
   neutral placeholder and file the "make it optional" change separately.

## Work items

1. **`api/seed.json`**: delete `sa-bcordes-bff` (clients[1]) and `_productionExampleClients`;
   rename clients[2] to `bff-example-client` / `bff-example-secret` (displayName "BFF Example").
   ⚠️ Deleting index 1 shifts `bff-example-client` to **index 1** — the seeder's redirect-URI
   rebasing in `docker/docker-compose.test.yml` is index-keyed (`Clients__2__*` → must become
   `Clients__1__*`). Update the stale "index 2" comment there too.
2. **`docker/docker-compose.test.yml`**: `OIDC_CLIENT_ID`/`OIDC_CLIENT_SECRET` on
   `bff-example`, the `Clients__*` overrides per item 1, and every `bcordes` comment.
3. **Tests**: retarget `SeedJsonBffClientTests` to the new id/secret; delete
   `SeedJsonProductionExampleTests`; drop the `sa-bcordes-bff` row from
   `SeederClientSecretMappingTests`; re-run `SeedClientIdConsistencyTests` (should only get
   simpler — production compose will inject exactly one clientId, defined by both seed files).
4. **Production surface**: remove the three `ClientSecrets__bcordes-*` lines
   (`docker-compose.production.yml`), the `BCORDES_*` block (`.env.production.example`), the
   BCORDES sentence in `scripts/prod-secrets.sh`, and the `${BCORDES_*}` note in
   `docker/CLAUDE.md`. `pnpm lint:env` enforces the compose ↔ example pairing — remove both
   sides together.
5. **`docker/seed.production.json`**: apply decision 5.
6. **Docs**: rewrite typescript-sdk.md's seeded-client section around `bff-example-client`;
   update testing-e2e.md, testing.md's service table, `apps/wallow-web/e2e/CLAUDE.md`, and
   deployment.md (drop the bcordes injection example; add the "create clients through the UI"
   paragraph per decision 4).
7. **Sweep**: `grep -rin bcordes` over the tree (excluding `docs/plans/`,
   `docs/agents/beads-archive/`, `CHANGELOG.md`, and git history) must come back empty —
   including `SECURITY.md`/`CONTRIBUTING.md`-style contact addresses only if they are seeding
   related; maintainer contact info is out of scope.

## Verification

- `./scripts/run-tests.sh all` — the four seed-pinning test files run in the fast tier.
- `pnpm check` — includes `lint:env` for the compose/example pairing.
- `./scripts/e2e.sh` — the cross-app `external-origin-login.spec.ts` consent journey is the
  live proof the renamed client still routes through real consent; the suite fails loudly if
  the index-shift in work item 1 is fumbled.

## Out of scope

- Building any new client-management UI (it exists).
- Reconciling the seeded "Wallow" organization with the first-run-setup-created organization —
  a real tension, but its own effort.
- Maintainer/contact addresses in community-health files.
