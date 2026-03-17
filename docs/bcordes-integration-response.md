# Foundry Backend Response: bcordes.dev Integration

**Date:** 2026-03-13
**From:** Foundry Backend Team
**To:** bcordes.dev Frontend Team

---

## Overview

We reviewed the integration requirements for bcordes.dev to use Foundry as its sole API backend. The goal is to replace the self-contained ORPC/Drizzle backend with Foundry's modular API, starting with two modules:

- **Showcases** — Read-only access to portfolio/showcase data via `GET /api/v1/showcases`
- **Inquiries** — Contact form submissions from anonymous visitors via a BFF service account, plus admin management and user-facing inquiry tracking

The frontend's BFF layer authenticates with Foundry using Keycloak client credentials (service account) and forwards requests on behalf of anonymous visitors. Authenticated users interact directly with Foundry using their JWT.

---

## API Overview

### Showcases

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/v1/showcases` | `showcases.read` scope | List all showcases for the tenant |

Service accounts with the `showcases.read` scope can call this endpoint. No user context required.

### Inquiries

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/api/v1/inquiries` | `inquiries.write` scope | Submit a new inquiry |
| `GET` | `/api/v1/inquiries` | `inquiries.read` permission | List all inquiries (admin) |
| `GET` | `/api/v1/inquiries/submitted` | Authenticated user | List the current user's submitted inquiries |
| `GET` | `/api/v1/inquiries/{id}` | `inquiries.read` or owner | View a specific inquiry |
| `PUT` | `/api/v1/inquiries/{id}/status` | `inquiries.write` | Update inquiry status |
| `POST` | `/api/v1/inquiries/{id}/comments` | `inquiries.write` | Add a comment to an inquiry |
| `GET` | `/api/v1/inquiries/{id}/comments` | Authenticated user | List comments (admin sees all, submitter sees external only) |

### Scopes

| Scope | Permission | Purpose |
|-------|------------|---------|
| `showcases.read` | `ShowcasesRead` | Read showcase data |
| `inquiries.read` | `InquiriesRead` | Read all inquiries (admin) |
| `inquiries.write` | `InquiriesWrite` | Submit inquiries, manage status, add comments |

All scopes are registered in `ApiScopes.ValidScopes` and mapped in `PermissionExpansionMiddleware`. They appear in the `GET /api/scopes` response.

---

## Implementation Status

All seven requirements from the integration spec have been implemented. Here is the detailed status:

### 1. Add Missing Scopes to ValidScopes — DONE

- `showcases.read`, `inquiries.read`, and `inquiries.write` are all registered in `ApiScopes.ValidScopes`
- Both inquiry scopes are mapped in `PermissionExpansionMiddleware` to `InquiriesRead` and `InquiriesWrite`
- `InquiriesRead` and `InquiriesWrite` exist in the `PermissionType` constants

### 2. Service Account Inquiry Submission — DONE

- `POST /api/v1/inquiries` accepts service account tokens with the `inquiries.write` scope
- Service account requests are detected by checking the `azp` claim for an `sa-` prefix
- When a service account submits, `SubmitterId` is set to `null`; when a regular user submits, `SubmitterId` is populated from the JWT `sub` claim
- All inquiry fields are stored: Name, Email, Company, ProjectType, BudgetRange, Timeline, Message
- `SubmitterIpAddress` is captured from the request

### 3. User Inquiry View Endpoint — DONE

- **Endpoint:** `GET /api/v1/inquiries/submitted` (not `/mine` — renamed per our preference)
- Requires an authenticated user (any role, no specific permission needed)
- Returns inquiries where `SubmitterId` matches the authenticated user's ID
- Service accounts calling this endpoint receive an empty array (expected behavior)

### 4. Inquiry Submitter Tracking — DONE

- `SubmitterId` is a nullable `string?` field on the `Inquiry` entity
- Populated from the JWT `sub` claim for authenticated user submissions
- Left `null` for service account submissions
- Used for ownership checks on `GET /api/v1/inquiries/{id}` and comment visibility

### 5. Inquiries Read Scope for Users — DONE

- `inquiries.read` scope is registered and mapped to `InquiriesRead`
- `GET /api/v1/inquiries` requires `InquiriesRead` permission (admin access to all inquiries)
- `GET /api/v1/inquiries/submitted` requires only authentication (scoped to own inquiries)
- `GET /api/v1/inquiries/{id}` allows access with `InquiriesRead` permission OR ownership

### 6. SignalR Events for Inquiries — DONE

Three SignalR handlers broadcast real-time events to tenant groups:

| Event | Type | Payload |
|-------|------|---------|
| Inquiry submitted | `InquirySubmitted` | `{ InquiryId, Name, Email }` |
| Status changed | `InquiryStatusUpdated` | `{ InquiryId, NewStatus }` |
| Comment added | `InquiryCommentAdded` | `{ InquiryId, CommentId, IsInternal }` |

All events use the `RealtimeEnvelope` format and are dispatched via the `ReceiveInquiries` SignalR method on the tenant group.

### 7. Inquiry Comments/Notes — DONE

- `InquiryComment` entity with fields: Id, InquiryId, AuthorId, AuthorName, Content, IsInternal, CreatedAt
- `POST /api/v1/inquiries/{id}/comments` — requires `inquiries.write`, accepts content and isInternal flag
- `GET /api/v1/inquiries/{id}/comments` — admin sees all comments, submitter sees only external comments (`IsInternal = false`), other users get 403
- SignalR event `InquiryCommentAdded` is broadcast when a comment is added

---

## Summary

| # | Feature | Status | Notes |
|---|---------|--------|-------|
| 1 | Add scopes to ValidScopes | Done | All three scopes registered and mapped |
| 2 | Service account inquiry submission | Done | `azp` claim detection for service accounts |
| 3 | User inquiry view endpoint | Done | `GET /api/v1/inquiries/submitted` |
| 4 | Submitter tracking field | Done | Nullable `SubmitterId` on Inquiry entity |
| 5 | Inquiries read scope | Done | `inquiries.read` with ownership fallback |
| 6 | SignalR events | Done | Three event types via `ReceiveInquiries` |
| 7 | Inquiry comments | Done | Internal/external visibility, SignalR events |

### Key Differences from Original Spec

- **`/submitted` instead of `/mine`** — The user inquiry endpoint is `GET /api/v1/inquiries/submitted`, not `/mine`
- **All items implemented** — Items 3-7 were listed as deferred but have been completed ahead of schedule

### Next Steps

The backend is ready for integration. The frontend team can:

1. Configure a Keycloak service account with `showcases.read` and `inquiries.write` scopes
2. Point the BFF layer at `POST /api/v1/inquiries` for contact form submissions
3. Point showcase data fetching at `GET /api/v1/showcases`
4. Connect SignalR for real-time inquiry updates when ready
