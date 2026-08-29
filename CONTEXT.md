# Wallow

A fork-first, multi-tenant base platform: a headless .NET API plus TypeScript frontends that
teams fork, rebrand, and extend. This glossary is the canonical vocabulary; use these terms in
issues, plans, tests, and code, and avoid the listed synonyms.

## Platform

**Fork**:
A team's downstream copy of this repository, rebranded and extended. Wallow itself is the
upstream base, never a deployed product.

**Module**:
One of the seven vertical backend slices (Identity, Storage, Notifications, Announcements,
Inquiries, ApiKeys, Branding). A module owns its data outright and talks to other modules only
through integration events.

**Integration event**:
A past-tense fact one module publishes for other modules to react to — the only way modules
communicate. A module-internal domain event is not an integration event.

**BFF**:
The server topology that owns the user's OIDC session and attaches tokens on the way to the
API; the browser never holds a token. wallow-web is the BFF app.
_Avoid_: calling every server layer a BFF — the distinction is session ownership.

**Passthrough**:
The session-less server topology: a pure reverse proxy that forwards requests to the API
verbatim and owns no session, cookie jar, or OIDC client. wallow-auth and minimal-app are
passthrough apps.
_Avoid_: BFF (a passthrough is not one)

**Ticket**:
A short-lived, single-use token that hands an authentication flow from one step to the next.
It is a protocol concept, not a stored entity.
_Avoid_: reusing "ticket" for support requests (see Inquiry)

## Tenancy & Identity

**Organization**:
The customer entity. It owns memberships and its registered clients, and it is the only word
that appears on the public surface (token claims, the SDK, registration screens) — "tenant"
never does. Today every organization is its own tenant.
_Avoid_: company, workspace, team, tenant (on anything a user or developer sees)

**Tenant**:
The isolation partition every scoped record names; "tenant" is the scoping word,
"organization" the domain noun. Today each organization is its own tenant, one to one, so a
tenant may later hold several organizations without the scoped records changing. There is no
separate Tenant entity yet.

**Organization context**:
The one organization a signed-in session acts within. A first-party client selects it (by an
organization hint at login, or the user's single membership); a developer application or
service account is bound to it at registration. A session with no organization context holds
no roles and reaches only what needs no organization: the person's profile, their
organizations, creating one, accepting an invitation.

**User**:
A person. A user deliberately belongs to no organization; all organizational facts about a
person live on their membership.
_Avoid_: account, member (as the entity)

**Membership**:
A person's relationship with one organization. Roles, status (pending, active, suspended,
denied), and ownership hang off the membership, never off the user. "Member" is informal
shorthand for a user with an active membership.

**Invitation**:
An emailed, tokened invite that brings a person into an organization.

**First-run setup**:
The platform state before any admin user exists. In this state the API serves only its setup,
health, and OIDC metadata surface and refuses everything else until setup completes.
_Avoid_: installation, onboarding (onboarding is per-user, not per-deployment)

**Bootstrap admin**:
The first admin user. Creating it mints the first organization and ends first-run setup.

**Enrollment policy**:
How an organization admits users: invite-only, request-approval, or open. For a developer
application it doubles as the application's sign-up policy: logging in through the
application enrolls the person in its organization under that policy.

**First-party client**:
One of the platform's own user-facing clients (client ids prefixed `wallow-`). Bound to no
organization and exempt from the consent screen; its organization context comes from the
login, never from registration. Only the platform can register one.
_Avoid_: internal app, trusted client

**Service account**:
A non-human OAuth2 client (client ids prefixed `sa-`) bound to exactly one organization and
acting only within it. API keys and scopes attach to service accounts, not people.
_Avoid_: bot, machine user

**Developer application**:
A third-party OAuth2 client (client ids prefixed `app-`) registered by an organization and
bound to exactly that organization, whose login and consent screens can carry its own client
branding. Always confidential: it runs a server-side backend.
_Avoid_: app (unqualified), external client, relying party (that is the protocol role, not the entity)

**Role**:
A named bundle of permissions granted by an organization — never by the platform. The built-in
roles are admin, manager, and user.

**Permission**:
A fine-grained capability, named `{Domain}{Action}`, expanded from a user's roles at request
time.

**Scope**:
A dotted string (e.g. `users.read`) limiting what a service account or API key may do; scopes
map onto permissions.

**API key**:
A hashed credential bound to a service account; it authenticates the service account, never a
person, and its scopes must be a subset of the account's.

## Communication

**Announcement**:
An admin-authored, tenant-scoped broadcast: targeted by audience, schedulable, expirable, and
dismissible per user.

**Notification**:
A single user's in-app item — one per recipient, with read and archive state. A published
announcement fans out into notifications.

**Channel**:
A delivery route for reaching a user: email, SMS, in-app, or push. Preferences are held per
channel and per notification type.

**Changelog entry**:
A global, tenant-less release note describing what changed in a version.

**Inquiry**:
A contact-form submission — lead capture, not a support ticket. Its status moves strictly
New → Reviewed → Contacted → Closed, and comments on it are either internal or
submitter-visible.
_Avoid_: support ticket, lead

## Storage

**Stored file**:
The metadata record for an uploaded file; the bytes live in the storage backend. "Object" is
the storage-backend term only, never the domain noun.
_Avoid_: document, blob, attachment

**Bucket**:
A logical grouping of stored files sharing settings: access level, provider, retention.

## Branding

**Fork branding**:
The build-time identity of a fork — app name, icon, links, and theme tokens — owned by the
fork's `branding.json`. Changing it is how a fork rebrands.

**Client branding**:
The runtime, per-developer-application skin (display name, tagline, logo, theme) shown on that
application's login and consent screens.
_Avoid_: organization branding — Identity's overlapping per-organization entity is a known
wart being reconciled, not a concept to build on

**Resolved branding**:
What a screen actually renders: fork branding merged with the requesting application's client
branding.
