# Docs Site

This folder contains the **DocFX documentation site** for Wallow. Everything here is user-facing documentation that will be published to the docs site.

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
                        #   frontend state, component library, forms
  operations/           # Deployment, versioning, observability, audit events,
                        #   reverse proxy, troubleshooting
  integrations/         # External auth, AsyncAPI, BFF pattern, TypeScript SDK
  api/                  # API reference docs (service accounts)
```

## Adding a New Guide

1. Create a lowercase kebab-case `.md` file in the appropriate category folder
2. Add an entry to `toc.yml` under the matching section
3. Use standard markdown — DocFX supports GitHub-flavored markdown

## Rules

- **Docs site content only** — no plans, designs, specs, or session artifacts
- **File naming** — always lowercase kebab-case (e.g., `api-development.md`)
- **Cross-references** — use relative paths (e.g., `../architecture/messaging.md`)
