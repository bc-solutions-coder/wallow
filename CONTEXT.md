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
API; the browser never holds a token. wallow-web and minimal-app (the external
relying-party example) are the BFF apps.
_Avoid_: calling every server layer a BFF — the distinction is session ownership.

**Passthrough**:
The session-less server topology: a pure reverse proxy that forwards requests to the API
verbatim and owns no session, cookie jar, or OIDC client. wallow-auth is the
passthrough app.
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

**Archived** (organization):
An organization's own reversible off-state: no member can act in it and none of its clients
can authorize or obtain tokens until it is reactivated. Distinct from deletion and from
platform suspension.
_Avoid_: suspended (that is a membership or client state), inactive, disabled

**Deleted** (organization):
An organization's irreversible end. Every credential it held or issued is revoked, and its
memberships, invitations, clients, and service accounts cease to exist; the people remain as
users with one organization fewer. Only an admin of the organization or the platform operator
may delete it, and a platform-suspended organization can be deleted only by the operator.
_Avoid_: removed, purged (that is the tenant purge that follows), deactivated, archived

**Tenant purge**:
The removal of every module's records scoped to a deleted organization's tenant — files,
notifications, inquiries, announcements, settings. It follows deletion and never precedes it;
deletion is safe without it, because purge removes data, not access.
_Avoid_: cascade (the credential revocation that deletion itself performs), cleanup

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

**Client**:
Any OAuth2 client registered with the platform: a first-party client, a developer
application, or a service account. "Client" alone is the umbrella; name the kind when it
matters.

**First-party client**:
One of the platform's own user-facing clients, declared by the seed's `firstParty` flag —
never inferred from the client id. Bound to no organization and exempt from the consent
screen; its organization context comes from the login, never from registration. Only the
seed can register one; every client registered at runtime is third-party.
_Avoid_: internal app, trusted client

**Service account**:
A non-human OAuth2 client (client ids prefixed `sa-`) registered by an organization, bound to
exactly that organization and acting only within it, under its own identity: what it does is
attributed to the service account, never to the member who created it. Its scopes are the
organization's to grant. Always confidential.
_Avoid_: bot, machine user, application (that is the user-facing client kind)

**Developer application**:
A third-party OAuth2 client (client ids prefixed `app-`) registered by an organization and
bound to exactly that organization, whose login and consent screens can carry its own client
branding. Always confidential: it runs a server-side backend.
_Avoid_: app (unqualified), external client, relying party (that is the protocol role, not the entity)

**Suspended** (client):
A registered client's reversible off-state: it cannot authorize or obtain tokens, and its
outstanding tokens are revoked, while its configuration, branding, and consents are kept.
The verbs are _suspend_ and _reinstate_. First-party clients are never suspended.
_Avoid_: disabled, deactivated, revoked (a client is suspended or deleted; tokens are revoked)

**Platform suspension**:
A suspension imposed by the platform operator on a client or an organization, carrying a
reason the organization's admins can read but cannot lift. It overrides the organization's
own state.

**Revocation**:
Invalidating a credential or grant so it no longer works: a token, a consent, an invitation,
or an API key. Never said of a client, service account, or organization — those are
suspended, archived, or deleted.

**Role**:
A named bundle of permissions granted by an organization — never by the platform. The built-in
roles are admin, manager, and user.

**Permission**:
A fine-grained capability, named `{Domain}{Action}`, expanded from a user's roles at request
time.

**Scope**:
A dotted string (e.g. `users.read`) limiting what a client (developer application or service
account) or an API key may do; scopes map onto permissions. A scope may be platform-only, in
which case no organization can grant it to a client.

**API key**:
A hashed credential bound to a person; whoever presents it acts _as_ that person, and its
scopes must be a subset of what that person may do. Never a service account's credential —
a service account authenticates with its client secret.
_Avoid_: service token, machine key

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

## Errors

**Problem**:
The wire body of any API error: the RFC 7807 problem+json document every non-OAuth endpoint
returns, carrying a status, a title, a code, a trace id, and optionally field errors. The
OAuth endpoints answer in the OAuth error shape instead, as the protocol requires.
_Avoid_: error response, error body, ProblemDetails (that is the ASP.NET type, not the concept)

**API failure**:
What a client holds after a request did not succeed: a problem parsed into one value, or a
network failure that never reached the API, so a screen handles both the same way. Carries
the code, status, trace id, and field errors; never the raw transport message.
_Avoid_: exception, fetch error, WallowError (the type name, not the concept)

**Failure message**:
The user-facing sentence a screen shows for an API failure, chosen by the code first and the
status second. The problem's detail is a fallback for client mistakes only; a network failure,
an unknown failure, or a server fault never shows its detail to a user.
_Avoid_: error message (ambiguous with the problem's detail), error text

**Field error**:
A failure message attached to one form field, keyed by the field's name as the frontend spells
it. Every other failure message on a form is the form's banner.
_Avoid_: validation error (a field error may come from a business rule, not validation)

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
The runtime, per-developer-application skin (display name, tagline, uploaded logo, curated
theme) worn by every screen inside that application's authorize transaction. Service accounts
have none; the error screen never wears it.
_Avoid_: organization branding — Identity's overlapping per-organization entity is a known
wart being reconciled, not a concept to build on

**Display name**:
The mutable, end-user-facing name of a developer application — the heading on its branded
screens and the name consent asks on behalf of. Defaults to the application's name at
registration; may never equal the fork's app name.
_Avoid_: name (that is the developer's immutable handle the client id derives from), title

**Authorize transaction**:
The span from an application's authorize request to the redirect back to it. Screens reached
inside it (login, register, consent, terms, MFA, forgot-password) inherit that application's
client branding; screens reached from an email link or with no application do not.

**Resolved branding**:
What a screen actually renders: fork branding merged with the requesting application's client
branding.
