# Containerized tanstack-min BFF example for the .NET E2E harness
# (docker/docker-compose.test.yml -> bff-example service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile can build the @bc-solutions-coder/sdk workspace dependency alongside
# the example. The example references the SDK via `file:../..`, so the on-disk
# layout packages/sdk (SDK) and apps/tanstack-min (example)
# is preserved inside the image.
FROM node:20-alpine

# --- Build the @bc-solutions-coder/sdk workspace dependency ---
WORKDIR /app/packages/sdk
COPY packages/sdk/package.json packages/sdk/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY packages/sdk/ ./
RUN npm run build

# --- Install and bundle the tanstack-min example ---
# `@bc-solutions-coder/sdk` (file:../..) resolves to the SDK built above; the browser client
# is bundled by Vite into public/app.js and the server runs under tsx.
WORKDIR /app/apps/tanstack-min
RUN npm install --no-audit --no-fund
RUN npm run build

EXPOSE 3000
ENV PORT=3000

CMD ["npm", "start"]
