# Code Coverage

Coverage is collected automatically by `./scripts/run-tests.sh` using the settings in `tests/coverage.runsettings`.

> **Important:** Never run `dotnet test --collect:"XPlat Code Coverage"` without `--settings tests/coverage.runsettings`. The runsettings file excludes generated code that would otherwise inflate uncovered line counts.

## Exclusions

The following are excluded from coverage:

- EF Core migrations (`*.Migrations.*`)
- `Program` and `Startup` classes
- Module registration extensions (`*.Extensions.*Module*`)
- Assembly info and generated code (`AssemblyInfo`, `Logging.g.cs`, `LoggerMessage.g.cs`, `RegexGenerator.g.cs`)
- Design-time and factory classes (`DesignTimeTenantContext`, `*DbContextFactory`)
- Test assemblies and benchmarks
- The Auth project (`[Wallow.Auth]*`)
- Anything decorated with `CompilerGeneratedAttribute` or `ExcludeFromCodeCoverageAttribute`

## Viewing Coverage Locally

```bash
# Run tests (coverage collected automatically)
./scripts/run-tests.sh

# Install report generator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

# Open
open coverage-report/index.html
```

## CI Threshold

The CI pipeline enforces a **90% line coverage** minimum. If coverage drops below this, the `merge-coverage` job fails and blocks the pipeline.
