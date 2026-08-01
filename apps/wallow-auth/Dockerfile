# Containerized wallow-auth SSR + reverse-proxy host for the .NET E2E harness
# (docker/docker-compose.test.yml -> wallow-auth service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile builds against the pnpm workspace. The app depends on
# @bc-solutions-coder/sdk via `workspace:*`, which pnpm resolves locally from
# packages/sdk — never from a registry — so the on-disk layout packages/sdk
# (SDK) and apps/wallow-auth (app) must be preserved inside the image.
#
# The image is built here rather than pulled in, and the build needs the full
# workspace install (vite, nitro and the workspace packages are all devDeps of
# something), so this is a single-stage build rather than a prod-deps-only split.

# Node 24 matches the repo's .nvmrc; corepack ships with the image and pins
# pnpm from the root package.json `packageManager` field (pnpm@10.20.0).
FROM node:24-slim

RUN corepack enable
WORKDIR /repo

# Copy the workspace manifests first so `pnpm install` layers cache on
# dependency changes rather than on every source edit. .npmrc is copied for the
# @bc-solutions-coder scoped-registry config; the workspace dep is resolved
# locally so no NODE_AUTH_TOKEN is needed at build time.
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc tsconfig.base.json tsconfig.build.base.json ./
COPY packages/auth/package.json packages/auth/
COPY packages/config/package.json packages/config/
COPY packages/env/package.json packages/env/
COPY packages/forms/package.json packages/forms/
COPY packages/lint/package.json packages/lint/
COPY packages/logger/package.json packages/logger/
COPY packages/query/package.json packages/query/
COPY packages/sdk/package.json packages/sdk/
COPY packages/styles/package.json packages/styles/
COPY packages/testing/package.json packages/testing/
COPY packages/ui/package.json packages/ui/
COPY packages/utils/package.json packages/utils/
COPY apps/wallow-auth/package.json apps/wallow-auth/

RUN pnpm install --frozen-lockfile

# The fork's branding is a BUILD input, not just a runtime one:
# @bc-solutions-coder/styles statically imports branding.json so Vite inlines it
# into both bundles. It used to need its own COPY line here, because it lived at
# api/branding.json — outside both apps/ and packages/. It now lives at
# packages/styles/branding.json, so the `COPY packages/styles packages/styles`
# below brings it in.

# Copy every workspace package plus the app sources, then build all package
# dist output the app imports followed by the app's own Start build. Every
# package dir must be present and built or `vite build` fails with
# UNRESOLVED_IMPORT.
#
# `workspace:*` DEV dependencies are copied too, and one of them is load-bearing:
# every package's own vite.config.ts imports @bc-solutions-coder/config, so
# omitting packages/config fails the package build below — not the app's — with
# ERR_MODULE_NOT_FOUND out of a .vite-temp config stub, which reads nothing like
# a missing COPY. This list is mirrored from the manifest by
# src/docker-workspace-copies.test.ts, so a new workspace dep fails there rather
# than minutes into an image build.
COPY packages/auth packages/auth
COPY packages/config packages/config
COPY packages/env packages/env
COPY packages/forms packages/forms
COPY packages/lint packages/lint
COPY packages/logger packages/logger
COPY packages/query packages/query
COPY packages/sdk packages/sdk
COPY packages/styles packages/styles
COPY packages/testing packages/testing
COPY packages/ui packages/ui
COPY packages/utils packages/utils
COPY apps/wallow-auth apps/wallow-auth

# The URL prefix this image serves under, empty by default (root-mounted, the
# behavior every existing stack expects); a path-based ingress builds with
# `--build-arg AUTH_BASE_PATH=/auth`. It is a BUILD input and not a runtime ENV
# because `vite build` bakes it into every emitted asset URL — a container
# started with a different value would still serve HTML pointing at the prefix it
# was built with. Vite reads process.env, which sees the ENV and not the ARG, so
# the promotion below is what actually carries it into the build.
ARG AUTH_BASE_PATH=""
ENV AUTH_BASE_PATH=${AUTH_BASE_PATH}

# Build all packages first (`pnpm --filter './packages/*' build` runs them in
# topological order, so a package is always built before any package that
# depends on it), then the app. The app's
# `pnpm build` is a single `vite build` that emits BOTH environments into
# .output/: .output/server/index.mjs is the entrypoint below and .output/public
# holds the client bundle and brand assets it serves. .output/ is gitignored, so
# it never arrives via the build context — it only exists if built here.
RUN pnpm --filter './packages/*' build \
    && pnpm --filter @bc-solutions-coder/wallow-auth build

WORKDIR /repo/apps/wallow-auth

# 3002 is wallow-auth's port (vite.config.ts DEFAULT_PORT); wallow-web uses 3000.
# The nitro server reads PORT at boot, so this is what it binds.
EXPOSE 3002
ENV PORT=3002

# The nitro server bundle, exec'd directly rather than through `pnpm start` so
# it is PID 1 and receives the container's stop signal itself.
CMD ["node", ".output/server/index.mjs"]
