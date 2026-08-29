# ADR 0001: Identity stays in-process, behind the OIDC seam

**Status:** accepted — 2026-08-29

## Context

Wallow is a fork-first modular monolith whose Identity module embeds OpenIddict 7 as a full
OpenID Provider: `/connect/authorize`, `/connect/token`, `/connect/userinfo`,
`/connect/logout`, discovery, and JWKS, with mandatory PKCE and a consent flow. External,
cross-domain clients are served the same way internal apps are — via the standard OIDC
redirect flow, with each relying party running its own confidential-client BFF (see
`docs/plans/2026-08-29/1254-external-idp-research.md` for the full spec-cited analysis).

The question arose whether to extract Identity into a separate application now, "for a
scalable future," so the platform would merely receive and verify tokens issued elsewhere.

Two facts decide it:

1. **OIDC already decouples verification from issuance.** Identity issues signed JWTs; every
   resource server validates them locally against the published JWKS. No runtime call to the
   identity system sits on the request hot path, whether the issuer runs in-process or on
   another continent. The scalability property extraction promises exists today.
2. **Physical extraction has real costs and no live need.** A second deployable, a second
   datastore (or shared-database coupling), doubled local-dev and e2e orchestration — and it
   breaks the platform's core premise that a fork ships one product with identity included,
   rather than a sidecar IdP (RFC 9700 and RFC 10017 recommend the architecture Wallow
   already has: embedded OP + per-RP BFF).

## Decision

Identity remains an in-process module of the monolith. What we protect instead is the
**seam**: every consumer of identity — internal apps included — integrates through the
standard OIDC surface, never through in-process shortcuts.

1. **Everything consumes Identity through the OIDC front door.** Wallow's own apps
   authenticate exactly like an external client would: authorization-code flow with PKCE,
   tokens, and local JWT validation against JWKS. No module reaches into Identity's services
   to mint, inspect, or trust a session by any other route.
2. **No other module touches Identity's persistence.** Cross-module needs are met by token
   claims or an explicit public contract, never by referencing Identity's tables or EF model.
3. **Identity's public surface stays small and standard.** Anything Identity exposes beyond
   the OIDC/OAuth endpoints and its own admin APIs is a seam leak; prefer expressing the need
   as a claim, scope, or documented contract.

## Consequences

Holding the seam keeps three futures equally cheap, and forks may pick any of them:

- **Use Wallow's IdP as shipped** — the default; nothing to do.
- **Migrate to a different OP** (Keycloak, FusionAuth, a corporate IdP): point consumers at
  the new issuer's discovery document and re-register clients. Because consumers depend only
  on the spec surface, no application code changes.
- **Extract Wallow's Identity module into its own host**: lift the module out, change the
  issuer URL in configuration. A deployment decision, not a rewrite — but one we defer until
  a real scaling or product need sends the bill.

The price is discipline: in-process shortcuts into Identity are forbidden even where they
would be convenient, and new cross-module features must be designed as claims or contracts.
`api/tests/Wallow.Architecture.Tests` is the natural home for enforcing rules 1–2
mechanically; add coverage there as violations become expressible.

The agent-facing copy of the seam rules lives in `.claude/rules/IDENTITY.md`; this ADR is
the authoritative statement.
