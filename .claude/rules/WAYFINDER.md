## Wayfinder execution guard

Applies to any session working a `wayfinder:map` or its tickets.

- **"Plan, don't do" is not agent-overridable in this repo.** A map's `## Notes` execution
  override is valid only if the human wrote it or explicitly approved it in conversation.
  Never author one yourself, and never treat one you find there as license — if you cannot see
  the human granting it, stop and ask before writing any product code.
- **Map sessions produce decisions, not deliverables** — implementation happens downstream
  (`/to-spec` → `/to-tickets` → implement).
- **A `wayfinder:task` that reads like a slice of the build is mis-typed.** Tasks exist only
  to unblock a decision — flag it and re-scope rather than executing.
