# Docs Site

DocFX site. Everything under `docs/` is user-facing site content EXCEPT `plans/`, `agents/`
and `research/` (agent research notes), kept off the build by `docfx.json`'s
`build.content[0].exclude` list.

## Adding a Guide

1. Lowercase kebab-case `.md` in the matching category folder.
2. Add a `toc.yml` entry — a file without one silently misses the sidebar.
3. Verify with `./scripts/docs-serve.sh` (builds and serves at http://localhost:5004).

Cross-references use relative paths (e.g. `../architecture/messaging.md`).
