# .NET dependency audit, September 6, 2026

Baseline: remote `main` at `994c38dc9056c5829a26f1129e809066569961b4`, including merged Dependabot PRs #217 and #218. Local checkout was older; Hangfire.AspNetCore 1.8.25 and AWSSDK.S3 4.0.102.5 below reflect main. Registry queries succeeded for all 111 central version declarations, the separate Aspire SDK, and four local tools. This is a version and compatibility review; it does not certify a restored dependency graph as vulnerability-free or prove upgrades by building them.

## Framework, compiler, and SDK

- Keep `net10.0`: .NET 10 is the current stable LTS, supported through November 14, 2028. Runtime, ASP.NET Core, EF Core, and the version-coupled Extensions packages are already at current 10.0.11. [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).
- Raise the SDK baseline from `10.0.100` to `10.0.400`; installed SDK observed was `10.0.302`. `rollForward: latestMinor` means the global.json value is a minimum selection baseline rather than an exact installed SDK. Latest stable SDK is 10.0.400 according to [Microsoft release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json). Coordinate local machines and CI before raising this minimum.
- Remove `<LangVersion>latest</LangVersion>` to use the target-framework default, or explicitly use C# 14. Microsoft discourages `latest` because compiler selection can change language behavior between machines. [C# configuration guidance](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/configure-language-version).
- Application assembly version `4.0.0` is a release-please product version, not an outdated .NET version; do not bump it to follow runtime numbering.

## Recommended upgrade groups

1. **Aspire alignment:** move the old `Aspire.AppHost.Sdk` 9.2.1 and hosting integrations 13.4.6 to 13.5.3 together. The old SDK is the largest configuration mismatch. Adopt `<Project Sdk="Aspire.AppHost.Sdk/13.5.3">` and remove the separate old SDK declaration and explicit `Aspire.Hosting.AppHost` reference according to the [Aspire 13 template migration](https://aspire.dev/whats-new/aspire-13/). Check central package management interactions, startup, JavaScript resources, PostgreSQL/Valkey resources, certificates, and AppHost tests.
2. **Runtime package updates:** Scalar 2.17.2; ApiDescription.Server 10.0.11; FeatureManagement 4.7.0; StackExchange.Redis 3.1.31; independently versioned Extensions packages 10.9.0. The 10.9.0 Extensions line is distinct from framework-coupled 10.0.11 packages; do not force every Microsoft package to the same number. Restore/build and exercise authentication, caching, and API schema generation.
3. **Wolverine and OpenIddict:** remote main already has all five Wolverine declarations at latest 6.33.0 and both OpenIddict declarations at latest 7.7.0. Keep them; older local versions are not outstanding upgrades. Its net10 EF integration requires EF >=10.0.4, satisfied by 10.0.11. [Wolverine EF package dependencies](https://www.nuget.org/packages/WolverineFx.EntityFrameworkCore/6.33.0). Keep EF 10.0.11 and Npgsql provider 10.0.3: the provider requires EF >=10.0.4 and <11.0.0. [Npgsql package metadata](https://api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/10.0.3/npgsql.entityframeworkcore.postgresql.nuspec). Verify message handling, persistence, outbox behavior, migrations, and runtime compilation.
4. **OpenTelemetry:** upgrade six stable packages to 1.18.0 together. EF Core and Redis instrumentation still only have beta releases, and Process still only has RC releases. Their corresponding current candidates are 1.18.0-beta.1 and 1.18.0-rc.1; keep these explicitly prerelease, verify export behavior, and do not describe the whole group as stable.
5. **Test tooling:** Test SDK 18.9.0, Testcontainers group 4.15.0, WireMock 2.15.0. NSubstitute 5.3.0→6.2.0 is a separate major migration; review [upstream breaking changes](https://github.com/nsubstitute/NSubstitute/blob/main/BreakingChanges.md) and run the full behavioral suite.
6. **Analyzers and Roslyn:** NetAnalyzers 10.0.400, Meziantou 3.0.219, and Roslynator 5.0.0 introduce diagnostics under warnings-as-errors; handle as a dedicated change. Roslyn's eleven 5.0.0 transitive pins can be considered for 5.9.0 only as a coordinated set, with dotnet-ef and Wolverine runtime compilation tested. Existing comments document a previous mixed-version TypeLoadException. StyleCop is already on its latest beta; stable 1.1.118 would be a downgrade.
7. **Optional test-framework modernization:** `xunit` 2.9.3 is current for that package ID, but the successor `xunit.v3` is 4.0.0. This is a package/project migration, not a regular `xunit` version bump. See [migration guide](https://xunit.net/docs/getting-started/v3/migration) and [successor registry](https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json).

## Transitive pins and audit limitations

Do not blanket-update security pins to their latest major. In particular, `Microsoft.AspNetCore.OpenApi` 10.0.11 requires `Microsoft.OpenApi` >=2.7.5 and <3.0.0; latest Microsoft.OpenApi 3.10.2 is incompatible with that declared range. Keep 2.10.0. [ASP.NET OpenAPI dependency metadata](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/10.0.11/microsoft.aspnetcore.openapi.nuspec).

MessagePack 3.1.8 and SQLite native library 3.53.3 also need parent-graph validation before replacing their existing 2.x security pins. No recommendation here removes a pin without proving that its parent resolves a patched version. The HtmlSanitizer beta warning in the central file is stale: the declaration already uses stable 9.2.1039.

NuGet vulnerability warnings are explicitly nonfatal in Directory.Build.props, and AppHost suppresses NU1902–NU1904. Passing builds alone therefore do not establish a clean vulnerability audit. A restored `dotnet list package --vulnerable --include-transitive` inspection is still required for the actual resolved graph.

## Complete declared package inventory

Latest stable values were queried directly from each package's linked NuGet flat-container index. “Direct” includes references from csproj, props, and targets; “central pin” means no explicit PackageReference was found, so the declaration alone does not establish actual resolved usage. “Candidate” requires restore/build/behavior verification, not a guarantee of compatibility.

| Package | Main version | Latest stable | Recommendation | Role |
| --- | --- | --- | --- | --- |
| [Asp.Versioning.Mvc](https://api.nuget.org/v3-flatcontainer/asp.versioning.mvc/index.json) | 10.2.1 | 10.2.1 | Keep | direct |
| [Asp.Versioning.Mvc.ApiExplorer](https://api.nuget.org/v3-flatcontainer/asp.versioning.mvc.apiexplorer/index.json) | 10.2.1 | 10.2.1 | Keep | direct |
| [Asp.Versioning.OpenApi](https://api.nuget.org/v3-flatcontainer/asp.versioning.openapi/index.json) | 10.2.3 | 10.2.3 | Keep | direct |
| [Audit.EntityFramework.Core](https://api.nuget.org/v3-flatcontainer/audit.entityframework.core/index.json) | 32.3.1 | 32.3.1 | Keep | direct |
| [Microsoft.AspNetCore.OpenApi](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.openapi/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.Extensions.ApiDescription.Server](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.apidescription.server/index.json) | 10.0.0 | 10.0.11 | Upgrade candidate | direct |
| [MimeKit](https://api.nuget.org/v3-flatcontainer/mimekit/index.json) | 4.17.0 | 4.17.0 | Keep | central pin |
| [Scalar.AspNetCore](https://api.nuget.org/v3-flatcontainer/scalar.aspnetcore/index.json) | 2.16.12 | 2.17.2 | Upgrade candidate | direct |
| [AspNetCore.HealthChecks.NpgSql](https://api.nuget.org/v3-flatcontainer/aspnetcore.healthchecks.npgsql/index.json) | 9.0.0 | 9.0.0 | Keep | direct |
| [AspNetCore.HealthChecks.Hangfire](https://api.nuget.org/v3-flatcontainer/aspnetcore.healthchecks.hangfire/index.json) | 9.0.0 | 9.0.0 | Keep | direct |
| [Serilog.AspNetCore](https://api.nuget.org/v3-flatcontainer/serilog.aspnetcore/index.json) | 10.0.0 | 10.0.0 | Keep | direct |
| [Serilog.Expressions](https://api.nuget.org/v3-flatcontainer/serilog.expressions/index.json) | 5.0.0 | 5.0.0 | Keep | direct |
| [Serilog.Sinks.Console](https://api.nuget.org/v3-flatcontainer/serilog.sinks.console/index.json) | 6.1.1 | 6.1.1 | Keep | direct |
| [OpenTelemetry.Extensions.Hosting](https://api.nuget.org/v3-flatcontainer/opentelemetry.extensions.hosting/index.json) | 1.16.0 | 1.18.0 | Upgrade candidate | direct |
| [OpenTelemetry.Exporter.OpenTelemetryProtocol](https://api.nuget.org/v3-flatcontainer/opentelemetry.exporter.opentelemetryprotocol/index.json) | 1.16.0 | 1.18.0 | Upgrade candidate | direct |
| [OpenTelemetry.Instrumentation.AspNetCore](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.aspnetcore/index.json) | 1.16.0 | 1.18.0 | Upgrade candidate | direct |
| [OpenTelemetry.Instrumentation.EntityFrameworkCore](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.entityframeworkcore/index.json) | 1.16.0-beta.1 | None | Prerelease candidate 1.18.0-beta.1 | direct |
| [OpenTelemetry.Instrumentation.Http](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.http/index.json) | 1.16.0 | 1.18.0 | Upgrade candidate | direct |
| [OpenTelemetry.Instrumentation.Process](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.process/index.json) | 1.16.0-rc.1 | None | Prerelease candidate 1.18.0-rc.1 | direct |
| [OpenTelemetry.Instrumentation.Runtime](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.runtime/index.json) | 1.16.0 | 1.18.0 | Upgrade candidate | direct |
| [OpenTelemetry.Instrumentation.StackExchangeRedis](https://api.nuget.org/v3-flatcontainer/opentelemetry.instrumentation.stackexchangeredis/index.json) | 1.16.0-beta.1 | None | Prerelease candidate 1.18.0-beta.1 | direct |
| [Serilog.Sinks.OpenTelemetry](https://api.nuget.org/v3-flatcontainer/serilog.sinks.opentelemetry/index.json) | 4.2.0 | 4.2.0 | Keep | direct |
| [Hangfire.Core](https://api.nuget.org/v3-flatcontainer/hangfire.core/index.json) | 1.8.25 | 1.8.25 | Keep | direct |
| [Hangfire.AspNetCore](https://api.nuget.org/v3-flatcontainer/hangfire.aspnetcore/index.json) | 1.8.25 | 1.8.25 | Keep | direct |
| [Hangfire.PostgreSql](https://api.nuget.org/v3-flatcontainer/hangfire.postgresql/index.json) | 1.21.1 | 1.21.1 | Keep | direct |
| [WolverineFx](https://api.nuget.org/v3-flatcontainer/wolverinefx/index.json) | 6.33.0 | 6.33.0 | Keep | direct |
| [WolverineFx.RuntimeCompilation](https://api.nuget.org/v3-flatcontainer/wolverinefx.runtimecompilation/index.json) | 6.33.0 | 6.33.0 | Keep | direct |
| [WolverineFx.FluentValidation](https://api.nuget.org/v3-flatcontainer/wolverinefx.fluentvalidation/index.json) | 6.33.0 | 6.33.0 | Keep | direct |
| [WolverineFx.EntityFrameworkCore](https://api.nuget.org/v3-flatcontainer/wolverinefx.entityframeworkcore/index.json) | 6.33.0 | 6.33.0 | Keep | direct |
| [WolverineFx.Postgresql](https://api.nuget.org/v3-flatcontainer/wolverinefx.postgresql/index.json) | 6.33.0 | 6.33.0 | Keep | direct |
| [FluentValidation](https://api.nuget.org/v3-flatcontainer/fluentvalidation/index.json) | 12.1.1 | 12.1.1 | Keep | direct |
| [FluentValidation.DependencyInjectionExtensions](https://api.nuget.org/v3-flatcontainer/fluentvalidation.dependencyinjectionextensions/index.json) | 12.1.1 | 12.1.1 | Keep | direct |
| [Microsoft.EntityFrameworkCore](https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.EntityFrameworkCore.Design](https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore.design/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.EntityFrameworkCore.Relational](https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore.relational/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Npgsql.EntityFrameworkCore.PostgreSQL](https://api.nuget.org/v3-flatcontainer/npgsql.entityframeworkcore.postgresql/index.json) | 10.0.3 | 10.0.3 | Keep | direct |
| [Microsoft.AspNetCore.DataProtection.StackExchangeRedis](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.dataprotection.stackexchangeredis/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.AspNetCore.Authentication.JwtBearer](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.authentication.jwtbearer/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.AspNetCore.Authentication.OpenIdConnect](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.authentication.openidconnect/index.json) | 10.0.11 | 10.0.11 | Keep | central pin |
| [Microsoft.FeatureManagement.AspNetCore](https://api.nuget.org/v3-flatcontainer/microsoft.featuremanagement.aspnetcore/index.json) | 4.6.0 | 4.7.0 | Upgrade candidate | direct |
| [OpenIddict.AspNetCore](https://api.nuget.org/v3-flatcontainer/openiddict.aspnetcore/index.json) | 7.7.0 | 7.7.0 | Keep | direct |
| [OpenIddict.EntityFrameworkCore](https://api.nuget.org/v3-flatcontainer/openiddict.entityframeworkcore/index.json) | 7.7.0 | 7.7.0 | Keep | direct |
| [Microsoft.AspNetCore.Identity.EntityFrameworkCore](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.identity.entityframeworkcore/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.Extensions.Identity.Stores](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.identity.stores/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.AspNetCore.Authentication.Google](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.authentication.google/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.AspNetCore.Authentication.MicrosoftAccount](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.authentication.microsoftaccount/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [AspNet.Security.OAuth.GitHub](https://api.nuget.org/v3-flatcontainer/aspnet.security.oauth.github/index.json) | 10.0.0 | 10.0.0 | Keep | direct |
| [AspNet.Security.OAuth.Apple](https://api.nuget.org/v3-flatcontainer/aspnet.security.oauth.apple/index.json) | 10.0.0 | 10.0.0 | Keep | direct |
| [Microsoft.AspNetCore.SignalR.Client](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.signalr.client/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.AspNetCore.SignalR.StackExchangeRedis](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.signalr.stackexchangeredis/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [StackExchange.Redis](https://api.nuget.org/v3-flatcontainer/stackexchange.redis/index.json) | 3.0.17 | 3.1.31 | Upgrade candidate | direct |
| [AspNetCore.HealthChecks.Redis](https://api.nuget.org/v3-flatcontainer/aspnetcore.healthchecks.redis/index.json) | 9.0.0 | 9.0.0 | Keep | direct |
| [RedisRateLimiting](https://api.nuget.org/v3-flatcontainer/redisratelimiting/index.json) | 1.2.1 | 1.2.1 | Keep | direct |
| [Microsoft.Extensions.Caching.Hybrid](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.caching.hybrid/index.json) | 10.8.0 | 10.9.0 | Upgrade candidate | direct |
| [Microsoft.Extensions.Caching.StackExchangeRedis](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.caching.stackexchangeredis/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [MailKit](https://api.nuget.org/v3-flatcontainer/mailkit/index.json) | 4.17.0 | 4.17.0 | Keep | direct |
| [AWSSDK.S3](https://api.nuget.org/v3-flatcontainer/awssdk.s3/index.json) | 4.0.102.5 | 4.0.102.5 | Keep | direct |
| [Microsoft.Extensions.Logging.Abstractions](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.logging.abstractions/index.json) | 10.0.11 | 10.0.11 | Keep | central pin |
| [Microsoft.Extensions.Configuration.Abstractions](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.configuration.abstractions/index.json) | 10.0.11 | 10.0.11 | Keep | central pin |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.dependencyinjection.abstractions/index.json) | 10.0.11 | 10.0.11 | Keep | central pin |
| [Microsoft.Extensions.Hosting](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.hosting/index.json) | 10.0.11 | 10.0.11 | Keep | central pin |
| [Microsoft.Extensions.Http](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.http/index.json) | 10.0.11 | 10.0.11 | Keep | central pin |
| [Microsoft.Extensions.Http.Resilience](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.http.resilience/index.json) | 10.8.0 | 10.9.0 | Upgrade candidate | direct |
| [Microsoft.Extensions.Resilience](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.resilience/index.json) | 10.8.0 | 10.9.0 | Upgrade candidate | direct |
| [Aspire.Hosting](https://api.nuget.org/v3-flatcontainer/aspire.hosting/index.json) | 13.4.6 | 13.5.3 | Upgrade as Aspire group | central pin |
| [Aspire.Hosting.AppHost](https://api.nuget.org/v3-flatcontainer/aspire.hosting.apphost/index.json) | 13.4.6 | 13.5.3 | Upgrade as Aspire group | direct |
| [Aspire.Hosting.JavaScript](https://api.nuget.org/v3-flatcontainer/aspire.hosting.javascript/index.json) | 13.4.6 | 13.5.3 | Upgrade as Aspire group | direct |
| [Aspire.Hosting.PostgreSQL](https://api.nuget.org/v3-flatcontainer/aspire.hosting.postgresql/index.json) | 13.4.6 | 13.5.3 | Upgrade as Aspire group | direct |
| [Aspire.Hosting.Redis](https://api.nuget.org/v3-flatcontainer/aspire.hosting.redis/index.json) | 13.4.6 | 13.5.3 | Upgrade as Aspire group | direct |
| [Aspire.Hosting.Testing](https://api.nuget.org/v3-flatcontainer/aspire.hosting.testing/index.json) | 13.4.6 | 13.5.3 | Upgrade as Aspire group | direct |
| [Microsoft.Extensions.ServiceDiscovery](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.servicediscovery/index.json) | 10.8.0 | 10.9.0 | Upgrade candidate | direct |
| [HtmlSanitizer](https://api.nuget.org/v3-flatcontainer/htmlsanitizer/index.json) | 9.2.1039 | 9.2.1039 | Keep | direct |
| [Humanizer.Core](https://api.nuget.org/v3-flatcontainer/humanizer.core/index.json) | 3.0.10 | 3.0.10 | Keep | central pin |
| [BenchmarkDotNet](https://api.nuget.org/v3-flatcontainer/benchmarkdotnet/index.json) | 0.15.8 | 0.15.8 | Keep | direct |
| [Microsoft.NET.Test.Sdk](https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/index.json) | 18.8.1 | 18.9.0 | Upgrade candidate | direct |
| [xunit](https://api.nuget.org/v3-flatcontainer/xunit/index.json) | 2.9.3 | 2.9.3 | Keep | direct |
| [xunit.runner.visualstudio](https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/index.json) | 4.0.0 | 4.0.0 | Keep | direct |
| [Bogus](https://api.nuget.org/v3-flatcontainer/bogus/index.json) | 35.6.5 | 35.6.5 | Keep | direct |
| [AwesomeAssertions](https://api.nuget.org/v3-flatcontainer/awesomeassertions/index.json) | 9.6.0 | 9.6.0 | Keep | direct |
| [NSubstitute](https://api.nuget.org/v3-flatcontainer/nsubstitute/index.json) | 5.3.0 | 6.2.0 | Major upgrade review | direct |
| [Testcontainers](https://api.nuget.org/v3-flatcontainer/testcontainers/index.json) | 4.14.0 | 4.15.0 | Upgrade candidate | direct |
| [Testcontainers.PostgreSql](https://api.nuget.org/v3-flatcontainer/testcontainers.postgresql/index.json) | 4.14.0 | 4.15.0 | Upgrade candidate | direct |
| [Testcontainers.Redis](https://api.nuget.org/v3-flatcontainer/testcontainers.redis/index.json) | 4.14.0 | 4.15.0 | Upgrade candidate | direct |
| [Microsoft.AspNetCore.Mvc.Testing](https://api.nuget.org/v3-flatcontainer/microsoft.aspnetcore.mvc.testing/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.EntityFrameworkCore.InMemory](https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore.inmemory/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.EntityFrameworkCore.Sqlite](https://api.nuget.org/v3-flatcontainer/microsoft.entityframeworkcore.sqlite/index.json) | 10.0.11 | 10.0.11 | Keep | direct |
| [Microsoft.Extensions.TimeProvider.Testing](https://api.nuget.org/v3-flatcontainer/microsoft.extensions.timeprovider.testing/index.json) | 10.8.0 | 10.9.0 | Upgrade candidate | direct |
| [WireMock.Net](https://api.nuget.org/v3-flatcontainer/wiremock.net/index.json) | 2.12.0 | 2.15.0 | Upgrade candidate | direct |
| [Otp.NET](https://api.nuget.org/v3-flatcontainer/otp.net/index.json) | 1.4.1 | 1.4.1 | Keep | central pin |
| [coverlet.collector](https://api.nuget.org/v3-flatcontainer/coverlet.collector/index.json) | 10.0.1 | 10.0.1 | Keep | direct |
| [NetArchTest.Rules](https://api.nuget.org/v3-flatcontainer/netarchtest.rules/index.json) | 1.3.2 | 1.3.2 | Keep | direct |
| [JetBrains.Annotations](https://api.nuget.org/v3-flatcontainer/jetbrains.annotations/index.json) | 2026.2.0 | 2026.2.0 | Keep | direct |
| [Microsoft.CodeAnalysis.NetAnalyzers](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.netanalyzers/index.json) | 10.0.302 | 10.0.400 | Upgrade candidate | direct |
| [StyleCop.Analyzers](https://api.nuget.org/v3-flatcontainer/stylecop.analyzers/index.json) | 1.2.0-beta.556 | 1.1.118 | Keep latest beta; stable is older | direct |
| [Meziantou.Analyzer](https://api.nuget.org/v3-flatcontainer/meziantou.analyzer/index.json) | 3.0.123 | 3.0.219 | Upgrade candidate | direct |
| [Roslynator.Analyzers](https://api.nuget.org/v3-flatcontainer/roslynator.analyzers/index.json) | 4.15.0 | 5.0.0 | Major upgrade review | direct |
| [Microsoft.CodeAnalysis](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.Common](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.common/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.CSharp](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.csharp/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.CSharp.Scripting](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.csharp.scripting/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.CSharp.Workspaces](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.csharp.workspaces/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.Scripting](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.scripting/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.Scripting.Common](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.scripting.common/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.VisualBasic](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.visualbasic/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.VisualBasic.Workspaces](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.visualbasic.workspaces/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.Workspaces.Common](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.workspaces.common/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [Microsoft.CodeAnalysis.Workspaces.MSBuild](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.workspaces.msbuild/index.json) | 5.0.0 | 5.9.0 | Coordinated Roslyn review | central pin |
| [MessagePack](https://api.nuget.org/v3-flatcontainer/messagepack/index.json) | 2.5.302 | 3.1.8 | Hold; parent compatibility review | central pin |
| [Microsoft.OpenApi](https://api.nuget.org/v3-flatcontainer/microsoft.openapi/index.json) | 2.10.0 | 3.10.2 | Hold; parent compatibility review | central pin |
| [SQLitePCLRaw.lib.e_sqlite3](https://api.nuget.org/v3-flatcontainer/sqlitepclraw.lib.e_sqlite3/index.json) | 2.1.12 | 3.53.3 | Hold; parent compatibility review | central pin |
| [Newtonsoft.Json](https://api.nuget.org/v3-flatcontainer/newtonsoft.json/index.json) | 13.0.4 | 13.0.4 | Keep | central pin |
| [Aspire.AppHost.Sdk](https://api.nuget.org/v3-flatcontainer/aspire.apphost.sdk/index.json) | 9.2.1 | 13.5.3 | Upgrade as Aspire group | SDK |
| [dotnet-ef](https://api.nuget.org/v3-flatcontainer/dotnet-ef/index.json) | 10.0.11 | 10.0.11 | Keep | tool |
| [dotnet-outdated-tool](https://api.nuget.org/v3-flatcontainer/dotnet-outdated-tool/index.json) | 4.8.1 | 4.8.1 | Keep | tool |
| [ilspycmd](https://api.nuget.org/v3-flatcontainer/ilspycmd/index.json) | 10.1.1.8388 | 11.0.0.9375 | Major upgrade review | tool |
| [docfx](https://api.nuget.org/v3-flatcontainer/docfx/index.json) | 2.78.5 | 2.78.5 | Keep | tool |
