## Identity seam rules

Identity is an in-process module, but every consumer must treat it as if it were a separate
IdP — so forks can use Wallow's IdP, migrate to a different OIDC provider, or extract the
module into its own host without rewriting consumers. Authoritative decision:
`docs/adr/0001-identity-behind-the-oidc-seam.md`.

- **Consume Identity only through the OIDC front door.** Internal apps authenticate like
  external clients: authorization-code flow + PKCE, tokens, local JWT validation against
  JWKS. Never mint, inspect, or trust a session via in-process calls into Identity.
- **Never touch Identity's persistence from another module** — no references to Identity
  tables or its EF model. Cross-module needs go through token claims or an explicit public
  contract.
- **Keep Identity's public surface small and standard.** New exposure beyond the OIDC/OAuth
  endpoints and Identity's own admin APIs is a seam leak — express the need as a claim,
  scope, or documented contract instead, and flag it if a task demands otherwise.
- **Do not extract Identity into a separate service** and do not propose it as a scalability
  fix — verification is already local (JWKS); extraction is a deferred deployment decision
  per the ADR.
