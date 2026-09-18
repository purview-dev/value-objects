# Agent Instructions

## Purpose and authority

This repository contains the `Purview.ValueObjects` package: runtime contracts, an incremental source generator,
a diagnostic analyzer, a code fix, tests, samples, and documentation for scalar and complex value objects.

- This file is the repository-wide source of truth for AI agents. More-specific `AGENTS.md` files, if added later,
  take precedence for their subtrees.
- Follow explicit user instructions first, then the nearest applicable repository instructions, then established
  code patterns.
- Operate only in this repository unless the user explicitly expands the scope.
- Never read, copy, log, or commit secrets from excluded files, environment variables, user profiles, local
  configuration, test output, or provider credentials.

## Source of truth and layout

| Path | Purpose |
| --- | --- |
| `src/ValueObjects.slnx` | Canonical solution for restore, build, test, and pack |
| `src/src/ValueObjects` | Runtime contracts (`[Scalar]`, `[ValueObject]`, `IValueObject`, `ScalarJsonConverterFactory`) |
| `src/src/SourceGenerator` | Roslyn incremental generator and analyzer metadata |
| `src/src/SourceGenerator.Refactorings` | Code fixes (add `partial` modifier) |
| `src/tests` | Unit and source-generator tests |
| `samples` | Runnable examples |
| `docs` | User-facing guidance |
| `Directory.Packages.props` | Centrally managed NuGet versions |
| `src/Directory.Build.props` / `src/Directory.Build.targets` | Solution-wide SDK, package, analyzer, and build behavior |
| `global.json` | Required .NET SDK and Microsoft.Testing.Platform selection |
| `package.json` | Authoritative repository/package version and Changesets package identity |
| `Justfile` | Supported local workflow commands |

## Standard workflow

1. Read this file, inspect the working tree, and locate the implementation, tests, documentation, and existing
   patterns relevant to the task.
2. Confirm behavior from code and tests rather than relying on memory or documentation alone.
3. Make the smallest coherent change. Preserve public behavior unless the task explicitly changes it.
4. Update tests for fixes and behavior changes. Update documentation when public behavior changes.
5. Run the narrowest meaningful validation first, then broader validation in proportion to risk.
6. Review the diff for unrelated edits, generated noise, compatibility risks, and missing docs or tests.

## Value object invariants

- Keep value objects immutable, validation deterministic, and equality/hash behavior aligned with every value that
  defines identity.
- Preserve the `Create` (strict) / `Hydrate` (replay-safe) split. `OnNormalize`/`OnValidate` hooks are the
  supported customization points.
- Preserve generated API shape: `Create`, `Hydrate`, `TryCreate`, `Empty`, comparison, equality, implicit
  conversions, enum properties, and JSON converters.
- Preserve `ValueObjectDeserializationMode` semantics (`Hydrate` default, `Strict` re-validates).
- Treat `[Scalar]`/`[ValueObject]`/`[ValueObjectDefaults]`, the interfaces, serialized payload shapes, diagnostic
  IDs, and generated method signatures as compatibility-sensitive contracts.
- Scalar value objects serialize as their underlying primitive value; do not change that without a schema/version
  strategy.

## Source generator rules

- The generator targets `netstandard2.0`; do not use APIs unavailable to that target.
- Follow Roslyn incremental-generator practices: derive output from declared inputs, keep transforms
  deterministic, avoid mutable global state and filesystem/environment dependencies, and make cancellation
  effective.
- Generated output must be stable for identical input.
- Diagnostics are public developer experience: preserve IDs and meanings, choose accurate locations and severity,
  and update `AnalyzerReleases.*.md` for newly introduced or changed diagnostics.
- Source-generator changes normally require tests in `src/tests/SourceGenerator.UnitTests`.

## Runtime project rules

- The runtime targets `net8.0;net9.0;net10.0`. Static abstract interface members (`IScalarValueObject`) require a
  net7+ floor.
- `ScalarJsonConverterFactory` uses only reflection over `ScalarAttribute` + `Create`/`Hydrate`/constructor;
  keep it free of event-sourcing dependencies.
- Keep the runtime package dependency-free (System.Text.Json is in-box for the supported TFMs).

## Testing conventions

- This repository uses TUnit on Microsoft.Testing.Platform, selected in `global.json`. Do not add xUnit, NUnit,
  MSTest, or FluentAssertions patterns unless explicitly requested.
- TUnit test methods use `[Test]`, and assertion calls are awaited, for example `await Assert.That(actual).IsEqualTo(expected);`.
- Use `--treenode-filter` for test filtering, not `dotnet test --filter`.
- Unit tests should cover domain logic, contracts, failure behavior, and regressions without external
  infrastructure.
- Source-generator tests assert generated code and diagnostics using the existing testing framework
  (`Purview.SourceGeneratorFramework.Testing.TUnit`).

## Documentation rules

- Keep `README.md`, package READMEs, and `docs/` aligned with actual supported behavior.
- Examples must compile conceptually against current public APIs. Use placeholders for credentials and
  environment-specific values.

## Build, format, test, and pack commands

Prefer the `Justfile` recipes or their equivalent commands:

```text
dotnet tool restore
just restore
just build
just test
just lint-check
just pack
just pipeline-pr
```

The repository uses the shared `Purview.Build` pipeline (`purview-build.json` at the root). `just pipeline-pr`
installs the pinned `Purview.Build` tool to `.tools/purview-build` when missing.

Local CI-equivalent validation:

```text
dotnet restore src/ValueObjects.slnx
dotnet build src/ValueObjects.slnx --no-restore --configuration Release
dotnet test src/ValueObjects.slnx --no-build --configuration Release --ignore-exit-code 8 -- --treenode-filter "/*/*/*/*[Category=Unit]"
dotnet csharpier check .
```

Use `dotnet csharpier check .` for validation and `dotnet csharpier format .` to fix formatting. Pack when package
assets, public package dependencies, analyzers, build targets, or packaging metadata change.

## Versioning, changesets, and releases

- `package.json` is the authoritative release/package version. Do not manually diverge project versions.
- User-facing package changes normally require a Changeset when release preparation is in scope.
- Release is automatic on push to `main`: the `Release` workflow runs the shared `Purview.Build` pipeline with
  `Release:Mode=NuGet`, publishing packages and creating the `v<version>` GitHub release only when that tag does
  not already exist.
- Never create release tags or publish packages manually unless the user explicitly requests a documented recovery
  procedure.

## Completion checklist

Before handing work back:

- Confirm the requested behavior and scope are satisfied.
- Confirm only intended files changed.
- Review public API, serialization, and package-content implications.
- Add or update focused tests for code changes.
- Update all affected docs, samples, analyzer metadata, and release notes when applicable.
- Run appropriate build, test, formatting, and pack checks in proportion to risk.
- State exactly what validation ran. If a check was skipped or blocked, give the concrete reason and remaining risk.