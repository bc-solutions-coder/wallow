# Docs Site

This folder contains the **DocFX documentation site** for Wallow. Everything here is user-facing
documentation that will be published to the docs site — with three deliberate exceptions,
`docs/plans/`, `docs/audits/`, and `docs/agents/`, which are tracked in git but kept off the site
by `docfx.json`'s `build.content[0].exclude` list (`plans/**`, `audits/**`, `agents/**`,
`claude/**`, `CLAUDE.md`).

## Structure

```
docs/
  index.md              # Landing page
  toc.yml               # Table of contents / sidebar navigation
  getting-started/      # Fork guide, developer guide, onboarding, configuration
  architecture/         # Assessment, module creation, authentication, authorization,
                        #   background jobs, caching, file storage, messaging, realtime
  development/          # API development, database development, database migrations,
                        #   testing (testing.md, testing-e2e.md), frontend setup,
                        #   frontend state, component library, forms, logging
  operations/           # Deployment, versioning, observability, request correlation,
                        #   audit events, reverse proxy, troubleshooting
  integrations/         # External auth, AsyncAPI, BFF pattern, TypeScript SDK,
                        #   integration cookbook
  api/                  # API reference docs (service accounts)
  plans/                # Session/design artifacts — excluded from the site build
  audits/               # Audit reports — excluded from the site build
  agents/               # Agent config (issue tracker, triage labels, domain docs) and the
                        #   beads archive — excluded from the site build
```

## Adding a New Guide

1. Create a lowercase kebab-case `.md` file in the appropriate category folder
2. Add an entry to `toc.yml` under the matching section
3. Use standard markdown — DocFX supports GitHub-flavored markdown
4. Verify it renders and its links resolve: `./scripts/docs-serve.sh` builds the site and serves it
   at <http://localhost:5004>

## Rules

- **Site pages are user-facing** — a guide, not a session artifact. Plans, audits, and agent
  config belong under `docs/plans/`, `docs/audits/`, and `docs/agents/`, which `docfx.json`
  excludes; nothing else here is excluded.
- **File naming** — always lowercase kebab-case (e.g., `api-development.md`)
- **Cross-references** — use relative paths (e.g., `../architecture/messaging.md`)
