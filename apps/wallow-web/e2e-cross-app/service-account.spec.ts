import { expect, test } from "@playwright/test";

/**
 * BACKEND dependent: the seeded `sa-wallow-nightly-sync` service account (api/seed.json)
 * mints a client-credentials token at the API's `/connect/token` and calls a tenant-scoped
 * endpoint with it. Needs the containerised stack: `scripts/e2e.sh` threads the API origin
 * through `E2E_API_URL`; by hand, `E2E_API_URL=http://localhost:5050`.
 * The identity harness stubs bearer auth, so this is the one place the org binding a
 * registered service account carries is proven against real JWT validation.
 */
const API_ORIGIN = process.env.E2E_API_URL ?? "http://localhost:5050";
const CLIENT_ID = "sa-wallow-nightly-sync";
const CLIENT_SECRET = "nightly-sync-secret";
const ORGANIZATION_NAME = "Wallow";
const OK = 200;

interface TokenResponse {
  access_token: string;
  scope?: string;
}

interface AccessTokenClaims {
  sub?: string;
  org_id?: string;
  scope?: string;
}

interface OrganizationSummary {
  id: string;
  name: string;
}

/** Decode the JWT payload (the API issues unencrypted access tokens). */
function decodePayload(token: string): AccessTokenClaims {
  const [, payload] = token.split(".");
  return JSON.parse(Buffer.from(payload ?? "", "base64url").toString("utf8")) as AccessTokenClaims;
}

test("a seeded service account authenticates and reaches its organization", async ({ request }) => {
  const tokenResponse = await request.post(`${API_ORIGIN}/connect/token`, {
    form: {
      grant_type: "client_credentials",
      client_id: CLIENT_ID,
      client_secret: CLIENT_SECRET,
      scope: "organizations.read",
    },
  });
  expect(tokenResponse.status()).toBe(OK);

  const token = (await tokenResponse.json()) as TokenResponse;
  const claims = decodePayload(token.access_token);
  expect(claims.sub).toBe(CLIENT_ID);
  expect(claims.org_id).toBeTruthy();
  expect(claims.scope).toContain("organizations.read");

  // A tenant-scoped call succeeds and answers the organization the token is bound to.
  const organizations = await request.get(`${API_ORIGIN}/v1/identity/organizations`, {
    headers: { Authorization: `Bearer ${token.access_token}` },
  });
  expect(organizations.status()).toBe(OK);

  const body = (await organizations.json()) as OrganizationSummary[];
  expect(body).toHaveLength(1);
  expect(body[0]?.id).toBe(claims.org_id);
  expect(body[0]?.name).toBe(ORGANIZATION_NAME);
});
