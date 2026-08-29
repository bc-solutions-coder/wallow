# PROTOTYPE — the external RP's image, built from the repo root so the
# workspace-linked SDK resolves. A real consumer's Dockerfile is the runtime
# stage alone plus `pnpm install` against GitHub Packages (NODE_AUTH_TOKEN).
FROM --platform=$BUILDPLATFORM node:24-slim AS build
RUN corepack enable
WORKDIR /repo
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc tsconfig.base.json tsconfig.build.base.json ./
COPY packages/sdk/package.json packages/sdk/
COPY packages/config/package.json packages/config/
COPY apps/minimal-app/package.json apps/minimal-app/
RUN pnpm install --frozen-lockfile --filter @bc-solutions-coder/example-minimal-app...
COPY packages/sdk packages/sdk
COPY packages/config packages/config
COPY apps/minimal-app apps/minimal-app
RUN pnpm --filter @bc-solutions-coder/example-minimal-app build

FROM node:24-slim
WORKDIR /app
COPY --from=build /repo/apps/minimal-app/.output ./.output
EXPOSE 3010
ENV PORT=3010
CMD ["node", ".output/server/index.mjs"]
