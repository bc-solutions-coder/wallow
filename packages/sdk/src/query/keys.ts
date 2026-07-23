/**
 * The single source of every TanStack Query key in the platform.
 *
 * RULES (enforced by convention + review, documented in CLAUDE.md):
 *  - No key literal (`["orgs", ...]`) may appear anywhere outside this file.
 *  - queryOptions factories AND invalidateQueries calls both reference these
 *    builders, so definition and invalidation can never drift.
 *  - Children are built FROM their parent (`[...detail(id), "members"]`) so
 *    invalidating a prefix (`queryKeys.organizations.all`) sweeps the subtree.
 *
 * Root literals preserve wallow-web's shipped keys (`orgs`, `apps`, `settings`,
 * `mfa`, `inquiries`) so adopting the factory does not orphan existing caches.
 */
export const queryKeys = {
  organizations: {
    all: ["orgs"] as const,
    detail: (id: string) => [...queryKeys.organizations.all, id] as const,
    members: (id: string) => [...queryKeys.organizations.detail(id), "members"] as const,
    clients: (id: string) => [...queryKeys.organizations.detail(id), "clients"] as const,
  },
  apps: {
    all: ["apps"] as const,
    detail: (clientId: string) => [...queryKeys.apps.all, clientId] as const,
  },
  settings: {
    all: ["settings"] as const,
    profile: () => [...queryKeys.settings.all, "profile"] as const,
  },
  mfa: {
    all: ["mfa"] as const,
    status: () => [...queryKeys.mfa.all, "status"] as const,
  },
  inquiries: {
    all: ["inquiries"] as const,
    detail: (id: string) => [...queryKeys.inquiries.all, id] as const,
    comments: (id: string) => [...queryKeys.inquiries.detail(id), "comments"] as const,
  },
  auth: {
    all: ["auth"] as const,
    currentUser: () => [...queryKeys.auth.all, "current-user"] as const,
    externalProviders: () => [...queryKeys.auth.all, "external-providers"] as const,
    clientTenant: (clientId: string) => [...queryKeys.auth.all, "client-tenant", clientId] as const,
    consentInfo: (clientId: string) => [...queryKeys.auth.all, "consent", clientId] as const,
    invitation: (token: string) => [...queryKeys.auth.all, "invitation", token] as const,
    verifyEmail: (email: string, token: string) =>
      [...queryKeys.auth.all, "verify-email", email, token] as const,
    redirectValidation: (url: string) =>
      [...queryKeys.auth.all, "redirect-validation", url] as const,
  },
};
