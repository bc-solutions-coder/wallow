# Workspace packages

Shared libraries live in this directory; application code lives under `apps/`.
Each package README describes its supported imports, setup, and examples. Public
TypeScript declarations carry editor documentation for package-owned APIs.

| Package                            | Purpose                                                           |
| ---------------------------------- | ----------------------------------------------------------------- |
| [api-errors](api-errors/README.md) | Parse API failures, classify error codes, and select messages     |
| [auth](auth/README.md)             | Current-user queries, permission checks, and route authentication |
| [config](config/README.md)         | Shared Vite app and library configuration                         |
| [env](env/README.md)               | Browser/server origin and base-path resolution                    |
| [forms](forms/README.md)           | Form context, fields, validation, and mutation submission         |
| [lint](lint/README.md)             | Wallow's oxlint plugin rules                                      |
| [logger](logger/README.md)         | Browser logging and server ingestion                              |
| [navigation](navigation/README.md) | Responsive application navigation and providers                   |
| [query](query/README.md)           | Query-client defaults and unhandled-failure reporting             |
| [sdk](sdk/README.md)               | Generated API client, authentication helpers, and Node BFF        |
| [styles](styles/README.md)         | Fork theme, assets, CSS, and Vite integration                     |
| [testing](testing/README.md)       | Vitest projects, browser assertions, and SDK test transports      |
| [ui](ui/README.md)                 | Shared components, hooks, and UI configuration                    |
| [utils](utils/README.md)           | Formatting, string, and value helpers                             |

`api-errors` and `sdk` are publishable packages. The others are private workspace
packages; consume them through workspace dependencies. The SDK integration guides
cover installing published versions in another repository.

Run a package command from the repository root with its scoped name:

```bash
pnpm --filter @bc-solutions-coder/forms typecheck
pnpm --filter @bc-solutions-coder/forms test
pnpm check
```

`pnpm check` runs the workspace quality checks, builds, tests, generation checks,
and package export validation. Generated SDK documentation comes from OpenAPI
and generator hooks. Update those owners rather than editing generated output.
