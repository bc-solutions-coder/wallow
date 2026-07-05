# Containerized tanstack-min BFF example for the .NET E2E harness
# (docker/docker-compose.test.yml -> bff-example service).
#
# IMPORTANT: the build context is the repo root (compose sets `context: ..`)
# so this Dockerfile can build the @wallow/sdk workspace dependency alongside
# the example. The example references the SDK via `file:../..`, so the on-disk
# layout packages/typescript-sdk (SDK) and packages/typescript-sdk/examples/
# tanstack-min (example) is preserved inside the image.
FROM node:20-alpine

# --- Build the @wallow/sdk workspace dependency ---
WORKDIR /app/packages/typescript-sdk
COPY packages/typescript-sdk/package.json packages/typescript-sdk/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY packages/typescript-sdk/ ./
RUN npm run build

# --- Install and bundle the tanstack-min example ---
# `@wallow/sdk` (file:../..) resolves to the SDK built above; the browser client
# is bundled by esbuild into public/app.js and the server runs under tsx.
WORKDIR /app/packages/typescript-sdk/examples/tanstack-min
RUN npm install --no-audit --no-fund
RUN npm run build

EXPOSE 3000
ENV PORT=3000

CMD ["npm", "start"]
