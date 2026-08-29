# Docs Site

This folder is the **DocFX documentation site** for Wallow. Everything here is user-facing
documentation — except `docs/plans/`, `docs/audits/`, and `docs/agents/`, which are tracked in
git but kept off the site by `docfx.json`'s `build.content[0].exclude` list (`plans/**`,
`audits/**`, `agents/**`, `claude/**`, `CLAUDE.md`).

## Structure

```
docs/
  index.md              # Landing page
  toc.yml               # Table of contents / sidebar navigation
  getting-started/      # Fork guide, developer guide, onboarding, configuration
  architecture/         # Module creation, auth, messaging, caching, storage, realtime, …
  development/          # API/database development, testing, frontend guides
  operations/           # Deployment, versioning, observability, troubleshooting, …
  integrations/         # External auth, AsyncAPI, BFF pattern, TypeScript SDK, cookbook
  api/                  # API reference docs (service accounts)
  plans/                # Session/design artifacts — excluded from the site build
  audits/               # Audit reports — excluded from the site build
  agents/               # Agent config + beads archive — excluded from the site build
```

## Adding a New Guide

1. Create a lowercase kebab-case `.md` file in the appropriate category folder
2. Add an entry to `toc.yml` under the matching section
3. Use standard markdown — DocFX supports GitHub-flavored markdown
4. Verify it renders and its links resolve: `./scripts/docs-serve.sh` builds the site and
   serves it at <http://localhost:5004>

## Rules

- **Site pages are user-facing** — a guide, not a session artifact. Plans, audits, and agent
  config belong under `docs/plans/`, `docs/audits/`, and `docs/agents/`; nothing else here is
  excluded from the build.
- **File naming** — always lowercase kebab-case (e.g., `api-development.md`)
- **Cross-references** — use relative paths (e.g., `../architecture/messaging.md`)
