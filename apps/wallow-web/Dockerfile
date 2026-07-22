# Containerized wallow-web BFF example for the .NET E2E harness
# (docker/docker-compose.test.yml -> bff-example service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile builds against the pnpm workspace. The example depends on
# @bc-solutions-coder/sdk via `workspace:*`, which pnpm resolves locally from
# packages/sdk — never from a registry — so the on-disk layout packages/sdk
# (SDK) and apps/wallow-web (example) must be preserved inside the image.
#
# The runtime runs `tsx server.ts` (a devDependency) directly against the TS
# source, so the full workspace install (including devDeps) is required at run
# time; this is a single-stage build rather than a prod-deps-only split.

# Node 24 matches the repo's .nvmrc; corepack ships with the image and pins
# pnpm from the root package.json `packageManager` field (pnpm@10.20.0).
FROM node:24-slim

RUN corepack enable
WORKDIR /repo

# Copy the workspace manifests first so `pnpm install` layers cache on
# dependency changes rather than on every source edit. .npmrc is copied for the
# @bc-solutions-coder scoped-registry config; the workspace dep is resolved
# locally so no NODE_AUTH_TOKEN is needed at build time.
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc tsconfig.base.json ./
COPY packages/sdk/package.json packages/sdk/
COPY packages/styles/package.json packages/styles/
COPY packages/testing/package.json packages/testing/
COPY packages/ui/package.json packages/ui/
COPY packages/web-shell/package.json packages/web-shell/
COPY apps/wallow-web/package.json apps/wallow-web/

RUN pnpm install --frozen-lockfile

# The fork's branding is a BUILD input: @bc-solutions-coder/styles statically
# imports api/branding.json (and src/lib/branding.ts re-exports it) so Vite
# inlines it into the bundle. It lives outside apps/ (the one source of fork
# identity shared with the .NET apps), so it must be copied in explicitly or
# `vite build` fails with UNRESOLVED_IMPORT.
COPY api/branding.json api/branding.json

# Copy every workspace package plus the example sources, then build all package
# dist output the example imports (@bc-solutions-coder/{sdk,styles,testing,ui,
# web-shell}) followed by the example's own client + SSR bundles. The example
# declares all five packages as `workspace:*` dependencies and its
# vite.config.ts imports @bc-solutions-coder/web-shell/server, so every package
# dir must be present and built or `vite build` fails with UNRESOLVED_IMPORT.
COPY packages/sdk packages/sdk
COPY packages/styles packages/styles
COPY packages/testing packages/testing
COPY packages/ui packages/ui
COPY packages/web-shell packages/web-shell
COPY apps/wallow-web apps/wallow-web

# Build all packages first (`pnpm --filter './packages/*' build` runs them in
# topological order, so @bc-solutions-coder/styles is built before
# @bc-solutions-coder/web-shell which depends on it), then the example.
RUN pnpm --filter './packages/*' build \
    && pnpm --filter @bc-solutions-coder/wallow-web build

WORKDIR /repo/apps/wallow-web

EXPOSE 3000
ENV PORT=3000

# `pnpm start` runs the example's `tsx server.ts` entrypoint.
CMD ["pnpm", "start"]
