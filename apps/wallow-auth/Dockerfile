# Containerized wallow-auth SSR + reverse-proxy host for the .NET E2E harness
# (docker/docker-compose.test.yml -> wallow-auth service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile builds against the pnpm workspace. The app depends on
# @bc-solutions-coder/sdk via `workspace:*`, which pnpm resolves locally from
# packages/sdk — never from a registry — so the on-disk layout packages/sdk
# (SDK) and apps/wallow-auth (app) must be preserved inside the image.
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
COPY apps/wallow-auth/package.json apps/wallow-auth/

RUN pnpm install --frozen-lockfile

# The fork's branding is a BUILD input, not just a runtime one: src/lib/branding.ts
# statically imports ../../../../api/branding.json so Vite inlines it into both
# bundles. It lives outside apps/ (it is the one source of fork identity shared
# with the .NET apps), so it must be copied in explicitly or `vite build` fails
# with UNRESOLVED_IMPORT. wallow-web copies it too, because @bc-solutions-coder/styles
# statically imports api/branding.json and both apps now consume that package.
COPY api/branding.json api/branding.json

# Copy the SDK, styles, and app sources, then build the SDK + styles dist the
# app imports (@bc-solutions-coder/sdk -> packages/sdk/dist,
# @bc-solutions-coder/styles -> packages/styles/dist) followed by the app's own
# client + SSR bundles.
COPY packages/sdk packages/sdk
COPY packages/styles packages/styles
COPY apps/wallow-auth apps/wallow-auth

# Both halves of `pnpm build` (vite build && vite build --config
# vite.ssr.config.ts) must land in the image and survive to run time:
# dist/client/ is served as static assets and dist/server/ssr.js is imported
# EAGERLY at boot by server.ts, which throws if it is absent. dist/ is
# gitignored, so it never arrives via the build context — it only exists if
# built here.
RUN pnpm --filter @bc-solutions-coder/sdk build \
    && pnpm --filter @bc-solutions-coder/styles build \
    && pnpm --filter @bc-solutions-coder/wallow-auth build

WORKDIR /repo/apps/wallow-auth

# 3002 is wallow-auth's port (server.ts DEFAULT_PORT); wallow-web uses 3000.
EXPOSE 3002
ENV PORT=3002

# `pnpm start` runs the app's `tsx server.ts` entrypoint.
CMD ["pnpm", "start"]
