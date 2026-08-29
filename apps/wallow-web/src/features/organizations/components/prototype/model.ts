/**
 * PROTOTYPE — throwaway. In-memory model of the merged org-scoped client
 * registration surface (map #112, ticket #122). Nothing here touches the API.
 *
 * The request/response shapes below ARE the thing being decided: the one
 * org-scoped route that replaces `AppsController` (app-, org-less) and
 * `ClientsController` (admin, org-bound).
 */

export type ClientKind = "application" | "service-account";

/** The merged self-service API, as this prototype proposes it. */
export const PROPOSED_API = {
  list: "GET    /v1/identity/organizations/{orgId}/clients",
  register: "POST   /v1/identity/organizations/{orgId}/clients",
  get: "GET    /v1/identity/organizations/{orgId}/clients/{clientId}",
  update: "PATCH  /v1/identity/organizations/{orgId}/clients/{clientId}",
  rotate: "POST   /v1/identity/organizations/{orgId}/clients/{clientId}/rotate-secret",
  remove: "DELETE /v1/identity/organizations/{orgId}/clients/{clientId}",
  permission: "OrganizationClientsManage (admin + manager)",
} as const;

/** What the platform states to every registrant. Decided value for the quickstart. */
export const PLATFORM = {
  issuer: "https://wallow.dev/auth",
  apiBaseUrl: "https://wallow.dev/api",
  quickstartUrl: "https://wallow.dev/docs/integrations/typescript-sdk#quickstart",
  serviceQuickstartUrl: "https://wallow.dev/docs/integrations/typescript-sdk#service-client",
} as const;

/** The org's grantable scope catalog. `platformOnly` scopes render but cannot be granted. */
export interface ScopeEntry {
  code: string;
  category: string;
  description: string;
  platformOnly?: boolean;
}

export const SCOPE_CATALOG: readonly ScopeEntry[] = [
  { code: "inquiries.read", category: "Inquiries", description: "Read inquiries" },
  { code: "inquiries.write", category: "Inquiries", description: "Submit and update inquiries" },
  { code: "announcements.read", category: "Communication", description: "Read announcements" },
  { code: "notifications.read", category: "Communication", description: "Read notifications" },
  { code: "storage.read", category: "Storage", description: "Read stored files" },
  { code: "storage.write", category: "Storage", description: "Upload files" },
  { code: "users.read", category: "Identity", description: "Read member profiles" },
  { code: "organizations.read", category: "Identity", description: "Read this organization" },
  { code: "users.manage", category: "Identity", description: "Delete users", platformOnly: true },
  { code: "configuration.manage", category: "Platform", description: "Platform configuration", platformOnly: true },
  { code: "webhooks.manage", category: "Platform", description: "Platform webhooks", platformOnly: true },
];

export const LOGIN_SCOPES = ["openid", "profile", "email", "offline_access"] as const;

export interface Branding {
  displayName: string;
  tagline: string;
}

/** The register request body (the decision under test). */
export interface RegisterClientRequest {
  kind: ClientKind;
  name: string;
  /** application only */
  redirectUris: string[];
  /** application only; editable later */
  postLogoutRedirectUris: string[];
  /** application only; editable later */
  backchannelLogoutUri: string | null;
  scopes: string[];
  /** application only; editable later */
  branding: Branding | null;
}

export interface ProtoClient extends RegisterClientRequest {
  id: string;
  clientId: string;
  createdAt: string;
  secretRotatedAt: string | null;
}

/** The register / rotate-secret response: the ONLY places a secret ever appears. */
export interface SecretReveal {
  client: ProtoClient;
  clientSecret: string;
  reason: "registered" | "rotated";
}

export const EMPTY_REQUEST: RegisterClientRequest = {
  kind: "application",
  name: "",
  redirectUris: [],
  postLogoutRedirectUris: [],
  backchannelLogoutUri: null,
  scopes: [],
  branding: null,
};

let counter = 3;

export function slug(name: string): string {
  return (
    name
      .toLowerCase()
      .replaceAll(/[^a-z0-9]+/g, "-")
      .replaceAll(/^-|-$/g, "") || "client"
  );
}

function fakeSecret(): string {
  const alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
  let out = "";
  for (let i = 0; i < 48; i += 1) out += alphabet[Math.floor(Math.random() * alphabet.length)];
  return out;
}

/** Stub of `POST .../clients`. */
export function stubRegister(orgSlug: string, request: RegisterClientRequest): SecretReveal {
  counter += 1;
  const prefix = request.kind === "application" ? "app" : "sa";
  const client: ProtoClient = {
    ...request,
    id: `proto-${counter}`,
    clientId: `${prefix}-${orgSlug}-${slug(request.name)}`,
    createdAt: new Date().toISOString(),
    secretRotatedAt: null,
  };
  return { client, clientSecret: fakeSecret(), reason: "registered" };
}

/** Stub of `POST .../clients/{clientId}/rotate-secret`. */
export function stubRotate(client: ProtoClient): SecretReveal {
  return {
    client: { ...client, secretRotatedAt: new Date().toISOString() },
    clientSecret: fakeSecret(),
    reason: "rotated",
  };
}

export function seedClients(orgSlug: string): ProtoClient[] {
  return [
    {
      id: "proto-1",
      kind: "application",
      clientId: `app-${orgSlug}-website`,
      name: "Website",
      redirectUris: ["https://bcordes.dev/bff/callback"],
      postLogoutRedirectUris: ["https://bcordes.dev/"],
      backchannelLogoutUri: "https://bcordes.dev/bff/backchannel-logout",
      scopes: ["inquiries.read", "notifications.read"],
      branding: { displayName: "bcordes.dev", tagline: "Sign in to bcordes.dev" },
      createdAt: "2026-08-01T10:00:00Z",
      secretRotatedAt: null,
    },
    {
      id: "proto-2",
      kind: "service-account",
      clientId: `sa-${orgSlug}-contact-form`,
      name: "Contact form",
      redirectUris: [],
      postLogoutRedirectUris: [],
      backchannelLogoutUri: null,
      scopes: ["inquiries.write"],
      branding: null,
      createdAt: "2026-08-02T10:00:00Z",
      secretRotatedAt: "2026-08-20T10:00:00Z",
    },
  ];
}

/** The ready-to-paste env block the reveal hands the developer. */
export function envBlock(reveal: SecretReveal | null, draft: RegisterClientRequest): string {
  const client = reveal?.client ?? draft;
  const secret = reveal?.clientSecret ?? "<revealed once after you register>";
  const clientId = reveal?.client.clientId ?? "<assigned when you register>";
  if (client.kind === "service-account") {
    return [
      `# Wallow service account — ${client.name || "unnamed"}`,
      `OIDC_ISSUER=${PLATFORM.issuer}`,
      `OIDC_SERVICE_CLIENT_ID=${clientId}`,
      `OIDC_SERVICE_CLIENT_SECRET=${secret}`,
      `OIDC_SERVICE_SCOPES=${client.scopes.join(" ") || "<pick at least one scope>"}`,
      `BFF_API_BASE_URL=${PLATFORM.apiBaseUrl}`,
    ].join("\n");
  }
  return [
    `# Wallow application — ${client.name || "unnamed"}`,
    `OIDC_ISSUER=${PLATFORM.issuer}`,
    `OIDC_CLIENT_ID=${clientId}`,
    `OIDC_CLIENT_SECRET=${secret}`,
    `OIDC_REDIRECT_URI=${client.redirectUris[0] ?? "<https://your-app.example/bff/callback>"}`,
    `OIDC_POST_LOGOUT_REDIRECT_URI=${client.postLogoutRedirectUris[0] ?? (client.redirectUris[0] ? new URL(client.redirectUris[0]).origin + "/" : "<https://your-app.example/>")}`,
    `OIDC_SCOPES=${[...LOGIN_SCOPES, ...client.scopes].join(" ")}`,
    `BFF_API_BASE_URL=${PLATFORM.apiBaseUrl}`,
    `COOKIE_PASSWORD=<run: openssl rand -base64 32>`,
  ].join("\n");
}

export function splitLines(value: string): string[] {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line !== "");
}
