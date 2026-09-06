import assert from "node:assert/strict";

import { isApiFailure } from "@bc-solutions-coder/api-errors";
import {
  createWallowSdk,
  getCurrentUser,
  loginRedirect,
  usersGetCurrentUser,
} from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserOptions } from "@bc-solutions-coder/sdk/query";
import { createWallowBffServer } from "@bc-solutions-coder/sdk/server";
import { createRequestOriginResolver } from "@bc-solutions-coder/sdk/server/forwarded";
import { createApiPassthrough } from "@bc-solutions-coder/sdk/server/passthrough";
import { createServiceClient } from "@bc-solutions-coder/sdk/server/service";

assert.equal(typeof createServiceClient, "function");
assert.equal(typeof createApiPassthrough, "function");
assert.equal(typeof createRequestOriginResolver, "function");
assert.ok(loginRedirect("/account").href.includes("returnTo=%2Faccount"));

const bff = createWallowBffServer({
  env: {
    OIDC_ISSUER: "https://platform.example.com/api",
    OIDC_CLIENT_ID: "app-example",
    OIDC_CLIENT_SECRET: "local-check-only",
    OIDC_REDIRECT_URI: "https://app.example.com/bff/callback",
    OIDC_POST_LOGOUT_REDIRECT_URI: "https://app.example.com/",
    OIDC_SCOPES: "openid profile email offline_access users.read",
    BFF_API_BASE_URL: "https://platform.example.com/api",
    COOKIE_PASSWORD: "local-check-only-at-least-32-characters",
  },
});
const HTTP_OK = 200;
const HTTP_UNAUTHORIZED = 401;
assert.equal(bff.handleHealth().status, HTTP_OK);
const anonymous = await bff.handleBff(new Request("https://app.example.com/bff/user"));
assert.equal(anonymous.status, HTTP_UNAUTHORIZED);

const sdk = createWallowSdk({
  baseUrl: "https://app.example.com/api",
  fetch: () => Promise.resolve(Response.json({ id: "external-user" })),
});
const user = await usersGetCurrentUser({ client: sdk.client });
assert.equal(user.id, "external-user");
assert.notDeepEqual(usersGetCurrentUserOptions({ client: sdk.client }).queryKey, []);
assert.equal(typeof getCurrentUser, "function");
assert.equal(isApiFailure(new Error("ordinary error")), false);
console.info("External consumer: all public entries load; BFF and typed API request pass.");
