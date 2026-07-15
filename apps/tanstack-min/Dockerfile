# Containerized tanstack-min BFF example for the .NET E2E harness
# (docker/docker-compose.test.yml -> bff-example service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile builds against the pnpm workspace. The example depends on
# @bc-solutions-coder/sdk via `workspace:*`, which pnpm resolves locally from
# packages/sdk — never from a registry — so the on-disk layout packages/sdk
# (SDK) and apps/tanstack-min (example) must be preserved inside the image.
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
COPY apps/tanstack-min/package.json apps/tanstack-min/

RUN pnpm install --frozen-lockfile

# Copy the SDK and example sources, then build the SDK dist the example imports
# (@bc-solutions-coder/sdk/server -> packages/sdk/dist) followed by the example's
# Vite client bundle (public/app.js).
COPY packages/sdk packages/sdk
COPY apps/tanstack-min apps/tanstack-min

RUN pnpm --filter @bc-solutions-coder/sdk build \
    && pnpm --filter @bc-solutions-coder/example-tanstack-min build

WORKDIR /repo/apps/tanstack-min

EXPOSE 3000
ENV PORT=3000

# `pnpm start` runs the example's `tsx server.ts` entrypoint.
CMD ["pnpm", "start"]
