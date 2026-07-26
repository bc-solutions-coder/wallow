/**
 * Organizations feature view types (Wallow-8w1h.4.2) — the CANONICAL shape the
 * list/detail components render. Mirrors the API `OrganizationDto`
 * (`packages/sdk` generated types); the facade returns these as `unknown`, so
 * the feature's components narrow to `Organization` at the render boundary.
 *
 * Later verticals (Apps/Settings/MFA/Inquiries, Phases 4-6) copy this file's
 * role: a small view-model type per feature the components consume.
 */
export interface Organization {
  id: string;
  name: string;
  domain: string | null;
  memberCount: string;
}

/**
 * An organization member (Wallow-8w1h.4.4) — mirrors the API `UserDto` the
 * `GET /v1/identity/organizations/{id}/members` op returns. The detail page's
 * `MemberList` narrows the facade's `unknown` payload to `OrganizationMember[]`
 * at the render boundary.
 */
export interface OrganizationMember {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  enabled: boolean;
  roles: string[];
}
