# Key Rotation

Wallow's identity provider signs and encrypts tokens with two X.509
certificates, seals cookies with ASP.NET Data Protection keys, and (on the BFF
side) seals its own session cookies with `COOKIE_PASSWORD(S)`. Each has a
different rotation story. This runbook covers what each key protects, what
breaks when it changes, and the procedure for rotating it.

## The key inventory

| Key | Where it lives | What it protects | Rotation |
| --- | --- | --- | --- |
| OpenIddict **signing** certificate | `signing.pfx` at `OpenIddict:SigningCertPath` (production: the `api_certs` volume, `/app/certs/signing.pfx`) | Signs access and identity tokens; its public key is published in the JWKS at `/.well-known/openid-configuration` → `jwks_uri` | Manual, swap-and-restart (below) |
| OpenIddict **encryption** certificate | `encryption.pfx` at `OpenIddict:EncryptionCertPath` (production: `/app/certs/encryption.pfx`) | Encrypts refresh tokens and authorization codes (access tokens are deliberately not encrypted — see [Access Token Format](../architecture/authorization.md#access-token-format)) | Manual, swap-and-restart (below) |
| `.pfx` passwords | `OPENIDDICT_SIGNING_CERT_PASSWORD` / `OPENIDDICT_ENCRYPTION_CERT_PASSWORD` in `.env.production` | Protect the certificate files at rest | Only together with a new certificate — the password is baked into the `.pfx` at generation time |
| ASP.NET **Data Protection** keys | Valkey/Redis, key `DataProtection-Keys` | Seal the IdP's own session and antiforgery cookies | Automatic — ASP.NET rotates them (~90 days) and keeps old keys for unprotection; no operator action |
| BFF **cookie seal password** | `COOKIE_PASSWORD` / `COOKIE_PASSWORDS` on each relying-party BFF | Seals the RP's session, transaction, and store-reference cookies | Keyed rotation without logging anyone out — see [Rotating the Cookie Password](../integrations/bff-pattern.md#rotating-the-cookie-password) |

In development none of this applies: OpenIddict uses its ephemeral development
certificates, regenerated per machine.

## How the certificates come to exist

On first startup in a non-development environment, if the configured `.pfx`
files are missing, the API **self-signs a 10-year 2048-bit RSA certificate for
each role** and writes it to the configured path, protected by the configured
password (`EnsureCertificateExists` in `IdentityInfrastructureExtensions`). In
the production compose stack those paths sit on the persistent `api_certs`
volume, so the certificates survive restarts and image upgrades.

Self-signed is correct here: nothing chains these certificates to a CA. Clients
trust the *keys* via the discovery document's JWKS, not the certificate chain.

## Rotating the signing or encryption certificate

Wallow currently loads **one** certificate per role, so rotation is
swap-and-restart: there is no overlap window where old and new keys are both
live. Know the blast radius before you start.

### Blast radius

- **Rotating `signing.pfx`** invalidates every outstanding access and identity
  token (default lifetimes: 15 and 10 minutes). API calls with an old access
  token start failing signature validation immediately.
- **Rotating `encryption.pfx`** invalidates every outstanding refresh token
  (default lifetime: 7 days) and any in-flight authorization codes. Sessions
  cannot silently refresh and must re-authenticate.
- **What survives:** the IdP's own session cookie is sealed with Data
  Protection keys in Valkey, not with these certificates. Because that session
  survives, a relying party whose tokens just died is bounced through
  `/connect/authorize`, gets silent SSO against the still-valid IdP session,
  and lands back signed in — users see a redirect flicker, not a login form.
- External relying parties that cache the JWKS re-fetch it when they see an
  unknown `kid`, so ID-token validation recovers on its own.

Practical consequence: rotating both certificates during quiet hours costs
users one silent re-auth. It is disruptive on the order of seconds, not a
log-everyone-out event.

### Procedure

1. Generate the replacement `.pfx` files. Easiest is to let the API regenerate
   them: with the stack stopped, remove the old files from the volume, then
   start the API. Update the password variables first if you are rotating those
   too — the new password is baked into the newly generated files.

   ```bash
   docker compose -f docker/docker-compose.production.yml stop wallow-api
   docker compose -f docker/docker-compose.production.yml run --rm --entrypoint sh wallow-api \
     -c 'rm /app/certs/signing.pfx /app/certs/encryption.pfx'
   docker compose -f docker/docker-compose.production.yml up -d wallow-api
   ```

   To bring your own key material instead, export a PKCS#12 bundle to the same
   paths (`openssl pkcs12 -export ...`), protected by the passwords from
   `.env.production`, before starting the API.

2. Verify the new key is being served:

   ```bash
   curl -s https://<host>/api/.well-known/openid-configuration | jq -r .jwks_uri
   curl -s <jwks_uri> | jq '.keys[].kid'
   ```

3. Confirm a login round-trip works (any RP, or the auth app itself). Expired
   BFF sessions self-heal through silent SSO; nothing else to clean up.

### If a private key is compromised

Rotate immediately as above, and additionally revoke standing grants: a stolen
signing key mints arbitrary access tokens until the JWKS no longer carries it,
so treat the rotation as the containment step and audit token issuance around
the suspected window (see [Audit Events](audit-events.md)). Restart **all** API
replicas together — a replica still holding the old signing key keeps minting
tokens the others reject, and vice versa.

## Multi-replica deployments

All API replicas must load the **same** certificate files — share the
`api_certs` volume or pre-provision identical `.pfx` files on every node. The
first-boot auto-generation is per-filesystem: two replicas with separate empty
volumes will each mint their own keys and reject each other's tokens.

## Future enhancement (not built)

Zero-flicker rotation needs the JWKS to publish the incoming signing key
*before* it starts signing, and the outgoing key to stay valid for verification
until the last old token expires. OpenIddict supports registering multiple
signing credentials, so this is an additive config change
(`OpenIddict:AdditionalSigningCertPaths` or similar) if the silent-SSO
re-auth ever becomes too disruptive. Until then, swap-and-restart is the
documented and supported path.
