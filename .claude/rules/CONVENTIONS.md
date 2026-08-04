## Coding Conventions

- **Never use `--` inside an XML comment** — in `.csproj`, `.props`, `.targets`, or any XML file.
  XML forbids `--` within `<!-- -->` and it causes MSB4025 parse errors. Rephrase CLI flags (write
  "reuse existing build output" instead of "use --no-build"). This one is restated here because it
  bites in any XML file, not only the ones an `api/` session has open.
- **Always ask for confirmation before deleting anything outside the current project.**

Every other language convention is owned by the toolchain's own file — read it there rather than a
copy:

- **C#** (no `var`, JWT-claim access, `[LoggerMessage]` logging, pre-commit formatting) —
  `api/CLAUDE.md`.
- **TypeScript** — `apps/CLAUDE.md` and each package's own `CLAUDE.md`; lint config detail is in
  `packages/lint/CLAUDE.md`.
