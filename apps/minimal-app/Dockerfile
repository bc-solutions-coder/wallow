# Containerized minimal-app — the external relying-party example the .NET E2E
# harness runs as its `bff-example` service (docker/docker-compose.test.yml).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`) so
# this Dockerfile builds against the pnpm workspace. The example depends on
# @bc-solutions-coder/sdk via `workspace:*`, which pnpm resolves locally from
# packages/sdk — never from a registry — so packages/sdk and apps/minimal-app
# must both be present inside the image. An external fork consuming the
# PUBLISHED SDK would instead copy only its own app and let `pnpm install` pull
# @bc-solutions-coder/sdk from GitHub Packages via a build secret
# (docs/integrations/typescript-sdk.md).
#
# Two stages, same shape as apps/wallow-web/Dockerfile: the `build` stage is
# pinned to $BUILDPLATFORM so `pnpm install` and `vite build` run natively even
# when the target image is another architecture (Vite 8's rolldown/oxc deadlock
# under QEMU); the runtime stage has no RUN steps at all.

# Node 24 matches the repo's .nvmrc; corepack pins pnpm from the root
# package.json `packageManager` field.
FROM --platform=$BUILDPLATFORM node:24-slim AS build

RUN corepack enable
WORKDIR /repo

# Manifests first so the install layer caches on dependency changes rather than
# on every source edit. Unlike the product apps, this app needs only three
# workspace packages: sdk and api-errors (its runtime workspace deps; the SDK's
# every failure is api-errors' `ApiFailure`) and config (the build-time vite
# preset). pnpm prunes the lockfile to the projects present.
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc tsconfig.base.json tsconfig.build.base.json ./
COPY packages/api-errors/package.json packages/api-errors/
COPY packages/config/package.json packages/config/
COPY packages/sdk/package.json packages/sdk/
COPY apps/minimal-app/package.json apps/minimal-app/

RUN pnpm install --frozen-lockfile

# Source next. In-repo every @bc-solutions-coder/* exports map resolves to the
# package's src/, so the app's `vite build` compiles workspace package source
# directly — nothing is pre-built.
COPY packages/api-errors packages/api-errors
COPY packages/config packages/config
COPY packages/sdk packages/sdk
COPY apps/minimal-app apps/minimal-app

# One build emits BOTH environments into .output/: .output/server/index.mjs is
# the entrypoint below and .output/public holds the client bundle.
RUN pnpm --filter @bc-solutions-coder/example-minimal-app build

# Runtime stage — built for the TARGET platform. Nitro's node-server preset
# bundles every runtime dependency into .output/server, so the bundle is the
# whole app.
FROM node:24-slim

WORKDIR /repo/apps/minimal-app

COPY --from=build /repo/apps/minimal-app/.output ./.output

# 3010 is this app's port (vite.config.ts DEFAULT_PORT). The nitro server reads
# PORT at boot, so this is what it binds.
EXPOSE 3010
ENV PORT=3010

# Exec'd directly rather than through `pnpm start` so it is PID 1 and receives
# the container's stop signal itself.
CMD ["node", ".output/server/index.mjs"]
