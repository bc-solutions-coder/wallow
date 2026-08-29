# Containerized wallow-auth SSR + reverse-proxy host for the .NET E2E harness
# (docker/docker-compose.test.yml -> wallow-auth service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile builds against the pnpm workspace. The app depends on
# @bc-solutions-coder/sdk via `workspace:*`, which pnpm resolves locally from
# packages/sdk — never from a registry — so the on-disk layout packages/sdk
# (SDK) and apps/wallow-auth (app) must be preserved inside the image.
#
# Two stages. The `build` stage is pinned to $BUILDPLATFORM so `pnpm install`
# and `vite build` always run NATIVELY on the machine doing the build, even when
# the target image is another architecture (CI cross-builds linux/arm64 on an
# amd64 runner). Vite 8's rolldown/oxc are multi-threaded native binaries, and
# running them under QEMU user-mode emulation deadlocked this app's build
# intermittently — a job that takes ~2 minutes natively sat silent for over an
# hour until the run was cancelled. The bundle Vite emits is platform-independent,
# so nothing is lost by building it on the host arch. Only the tiny runtime
# stage below is built for $TARGETPLATFORM, and it has no RUN steps at all.
#
# The build stage needs the full workspace install (vite, nitro and the workspace
# packages are all devDeps of something), so there is no prod-deps-only split —
# the runtime stage copies just the self-contained `.output/` bundle.

# Node 24 matches the repo's .nvmrc; corepack ships with the image and pins
# pnpm from the root package.json `packageManager` field (pnpm@11.24.0).
FROM --platform=$BUILDPLATFORM node:24-slim AS build

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

# Copy every workspace package plus the app sources. Every package dir must be
# present (source is enough — in-repo the exports maps resolve to src/, so no
# package is pre-built) or `vite build` fails with UNRESOLVED_IMPORT.
#
# `workspace:*` DEV dependencies are copied too, and one of them is load-bearing:
# every package's own vite.config.ts imports @bc-solutions-coder/config, so
# omitting packages/config fails the package build below — not the app's — with
# ERR_MODULE_NOT_FOUND out of a .vite-temp config stub, which reads nothing like
# a missing COPY. Nothing checks the list against the manifest any more --
# src/docker-workspace-copies.test.ts went with the rest of the source-reading
# guards (Wallow-xg9t.1) -- so adding a workspace dependency means adding two COPY
# lines here by hand, and forgetting fails minutes into an image build.
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

# Only the app builds — no `pnpm --filter './packages/*' build` first. In-repo,
# every @bc-solutions-coder/* exports map points at the package's src/, so the
# app's `vite build` compiles workspace package source directly; nothing
# resolves through dist/. That one build emits BOTH environments into .output/:
# .output/server/index.mjs is the entrypoint below and .output/public holds the
# client bundle and brand assets it serves. .output/ is gitignored, so it never
# arrives via the build context — it only exists if built here.
RUN pnpm --filter @bc-solutions-coder/wallow-auth build

# Runtime stage — built for the TARGET platform. Nitro's node-server preset
# bundles every runtime dependency into .output/server (_chunks/, _libs/; no
# node_modules/ and no native .node addons), so the bundle is the whole app and
# nothing from the workspace install is needed at runtime. Keep this stage free
# of RUN steps: that is what lets the arm64 image build without QEMU ever
# executing a foreign binary.
FROM node:24-slim

WORKDIR /repo/apps/wallow-auth

COPY --from=build /repo/apps/wallow-auth/.output ./.output

# 3002 is wallow-auth's port (vite.config.ts DEFAULT_PORT); wallow-web uses 3000.
# The nitro server reads PORT at boot, so this is what it binds.
EXPOSE 3002
ENV PORT=3002

# The nitro server bundle, exec'd directly rather than through `pnpm start` so
# it is PID 1 and receives the container's stop signal itself.
CMD ["node", ".output/server/index.mjs"]
