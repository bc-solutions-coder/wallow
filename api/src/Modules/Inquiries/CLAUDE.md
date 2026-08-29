# Inquiries module

- Command and event handlers are **static classes** with a static `HandleAsync` — a deliberate
  local exception to the repo's sealed-class handler convention; do not "fix" them. Query
  handlers use the usual sealed-class shape.
- Consumes `EmailVerifiedEvent` (Identity): `EmailVerifiedInquiryLinkHandler` auto-links
  anonymous inquiries to the verified user.
- Config keys: `Inquiries:AdminEmail`, `Inquiries:AdminUserIds`.
