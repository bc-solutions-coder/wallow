## Coding conventions

- **Never use `--` inside an XML comment** — in any XML file (`.csproj`, `.props`, `.targets`,
  …); it causes MSB4025 parse errors. Rephrase CLI flags in prose ("reuse existing build
  output", not "use --no-build").
- **Always ask for confirmation before deleting anything outside the current project.**
