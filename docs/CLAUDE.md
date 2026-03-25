# Docs Site

This folder contains the **DocFX documentation site** for Wallow. Everything here is user-facing documentation that will be published to the docs site.

## Structure

```
docs/
  index.md              # Landing page
  toc.yml               # Table of contents / sidebar navigation
  getting-started/      # Fork guide, developer guide, onboarding, configuration
  architecture/         # Module creation, messaging, caching, authorization, etc.
  development/          # API development, database, testing, frontend setup
  operations/           # Deployment, versioning, observability, troubleshooting
  integrations/         # External auth, DCR, AsyncAPI
  api/                  # API reference docs (service accounts, etc.)
```

## Adding a New Guide

1. Create a lowercase kebab-case `.md` file in the appropriate category folder
2. Add an entry to `toc.yml` under the matching section
3. Use standard markdown — DocFX supports GitHub-flavored markdown

## Rules

- **Docs site content only** — no plans, designs, specs, or session artifacts
- **Agent-generated docs** (module creation guides, AI instructions) go in `.claude/docs/`, not here
- **File naming** — always lowercase kebab-case (e.g., `api-development.md`)
- **Cross-references** — use relative paths (e.g., `../architecture/messaging.md`)
