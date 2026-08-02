## Coding Conventions

- **Never use `--` inside an XML comment** — in `.csproj`, `.props`, `.targets`, or any XML file.
  XML forbids `--` within `<!-- -->` and it causes MSB4025 parse errors. Rephrase CLI flags (write
  "reuse existing build output" instead of "use --no-build").
- **Always ask for confirmation before deleting anything outside the current project.**

C# conventions (no `var`, JWT-claim access, `[LoggerMessage]` logging, pre-commit formatting) live
in `api/CLAUDE.md`.
