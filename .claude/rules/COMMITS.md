## Commit Messages

All commit messages MUST follow [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>[optional scope][!]: <description>
```

**Allowed types:** `feat`, `fix`, `chore`, `refactor`, `docs`, `test`, `ci`, `style`, `perf`, `build`

**Version impact:** `fix:` = patch, `feat:` = minor, `!` or `BREAKING CHANGE` footer = major

**Rules:**
- Description must be lowercase, imperative mood, no period at end
- First line must be under 72 characters
- Use module name as scope when relevant: `feat(billing): add invoice export`

## Pre-Commit

**Before every commit**, run `dotnet format` on the solution to ensure consistent code style:

```bash
dotnet format Wallow.slnx
```

Stage any formatting changes before committing. Never commit unformatted code.
