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
| `src/tests` | Unit and source-generator tests, plus the multi-framework Entity Framework Core compatibility project |
| `samples` | Runnable examples |
| `docs` | User-facing guidance |
| `Directory.Packages.props` | Centrally managed NuGet versions |
| `src/Directory.Build.props` / `src/Directory.Build.targets` | Solution-wide SDK, package, analyzer, and build behavior |
| `global.json` | Required .NET SDK and Microsoft.Testing.Platform selection |
| `package.json` | Authoritative repository/package version |
| `Justfile` | Supported local workflow commands |
| `skills` | Authored agent skills, installable with the skills CLI (see [Agent skills](#agent-skills)) |
| `.agents/skills`, `skills-lock.json` | Skills installed from other repositories, committed (see [Installed skills](#installed-skills-agentsskills)) |

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
- Cross-component contracts must go through **public** members. The artifact that consumers (and Visual Studio)
  load is the merged, self-contained assembly the `Purview.SourceGeneratorFramework` merge pass produces, and
  that pass strips every `InternalsVisibleTo` declaration. A component that reads another component's internals
  (for example a code fix reading a diagnostic descriptor) therefore compiles against the unmerged build output
  and throws `FieldAccessException` at runtime in the IDE. Do not add `InternalsVisibleTo` between Roslyn
  components; expose the shared identity publicly (see `DiagnosticLibrary`).
- Source-generator changes normally require tests in `src/tests/SourceGenerator.UnitTests`.

## Runtime project rules

- The runtime targets `net8.0;net9.0;net10.0;net11.0`, selected through `PurviewTargetFrameworkSet=All` rather
  than a literal list, so the set moves with `Purview.BuildSdk`. Static abstract interface members
  (`IScalarValueObject`) require a net7+ floor.
- `ScalarJsonConverterFactory` uses only reflection over `ScalarAttribute` + `Create`/`Hydrate`/constructor;
  keep it free of event-sourcing dependencies.
- Keep the runtime package dependency-free (System.Text.Json is in-box for the supported TFMs).
- Purview.ZodSharp 3 ships `net10.0`/`net11.0` only. A project that references it selects
  `PurviewTargetFrameworkSet=Supported`; the runtime package does not reference it and keeps `All`.

## Trimming and Native AOT (non-negotiable)

- The generated, default path for every value object must stay trimming- and Native AOT-safe: no reflection, no
  `Type.MakeGenericType`, no expression-tree compilation, no reflection-based `JsonSerializer` overloads, and no
  unannotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` call sites.
- The runtime package sets `IsAotCompatible`, so the trim and AOT analyzers run as part of every build. A new
  trim/AOT warning is a defect, not noise.
- `ScalarJsonConverterFactory` is the only permitted reflection-based component. It must keep its
  `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` annotations at the opt-in construction point and its
  suppressions accurate. Do not add reflection anywhere else in the runtime.
- Never weaken or delete an AOT/trim annotation or suppression to silence a warning; fix the call site or move the
  reflection into the annotated factory.
- A feature is not complete until the generated path is confirmed clean under the trim/AOT analyzers. The in-repo
  projects never trim-analyze generated code, so the proof is the packed-consumer gate (`just smoke-packed-consumers`,
  `scripts/test-packed-consumers.ps1`): it publishes a Native AOT console and a Blazor WebAssembly app (trimmed and
  `RunAOTCompilation`) from the packed package with `-warnaserror`, runs them, and leak-scans every output.
- Generated JSON converters read and write through `ValueObjectJson` (runtime), never through the
  `JsonSerializer` overloads that take only `JsonSerializerOptions`.

## Testing conventions

- This repository uses TUnit on Microsoft.Testing.Platform, selected in `global.json`. Do not add xUnit, NUnit,
  MSTest, or FluentAssertions patterns unless explicitly requested.
- TUnit test methods use `[Test]`, and assertion calls are awaited, for example `await Assert.That(actual).IsEqualTo(expected);`.
- Use `--treenode-filter` for test filtering, not `dotnet test --filter`.
- Unit tests should cover domain logic, contracts, failure behavior, and regressions without external
  infrastructure.
- Entity Framework Core version differences are covered by
  `src/tests/ValueObjects.EFCompatibility.IntegrationTests`, which targets `net8.0` with EF Core 7, `net9.0`
  with EF Core 9, and `net10.0` with EF Core 10. Add version-dependent behavior there (and, where a synthetic
  reference set is enough, to `SourceGenerator.IntegrationTests`) rather than weakening generated output gated
  on `IsEF8Referenced`.
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
just smoke-packed-consumers
just verify-pack-rules
```

`just smoke-packed-consumers` is the packed-consumer gate (Native AOT, Blazor WebAssembly trimmed and AOT in a
headless browser, Blazor Server, net8.0, compiler-host leak scan). It needs the Native AOT toolchain, the
`wasm-tools` workload and Chrome or Edge; dependency versions not yet on nuget.org restore from
`LOCAL_NUGET_FEED_PATH`. Run it when generated code, the runtime, packaging or a dependency version changes.

The repository uses the shared `Purview.Build` pipeline (`purview-build.json` at the root). `just pipeline-pr`
runs `dotnet tool restore`, which installs the pinned `Purview.Build` from `.config/dotnet-tools.json`.

Local CI-equivalent validation:

```text
dotnet restore src/ValueObjects.slnx
dotnet build src/ValueObjects.slnx --no-restore --configuration Release
dotnet test src/ValueObjects.slnx --no-build --configuration Release --ignore-exit-code 8 -- --treenode-filter "/*/*/*/*[Category=Unit]"
dotnet csharpier check .
```

Use `dotnet csharpier check .` for validation and `dotnet csharpier format .` to fix formatting. Pack when package
assets, public package dependencies, analyzers, build targets, or packaging metadata change.

## Versioning and releases

- `package.json` is the authoritative release/package version. Do not manually diverge project versions.
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
- Confirm trimming/Native AOT safety: the generated path adds no reflection and no new
  `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` call sites, and the `IsAotCompatible` build is clean.
- Add or update focused tests for code changes.
- Update all affected docs, samples, analyzer metadata, and release notes when applicable.
- Run appropriate build, test, formatting, and pack checks in proportion to risk.
- State exactly what validation ran. If a check was skipped or blocked, give the concrete reason and remaining risk.

## Installed skills (`.agents/skills/`)

Specialist guidance this repository depends on - `Purview.BuildSdk` configuration, the
`Purview.SourceGeneratorFramework` generator and test APIs, and the ZodSharp integration - is published by the
repositories that own it, and installed here by the skills CLI. The installed skills are **committed**:
`.agents/skills/**` and the CLI's `skills-lock.json` (source, skill path and content hash of every entry) are both
in source, so a clone has them without running anything.

| Skill | Source | Load before |
| --- | --- | --- |
| `purview-buildsdk-*` (8 skills) | `purview-dev/build-sdk` | build/SDK configuration, project layout and detection, testing, skill installation and skill tests |
| `purview-sourcegenerator-codewriter` | `purview-dev/sourcegenerator-framework` | writing or reviewing emitter code (`CodeWriter`, scopes) |
| `purview-sourcegenerator-pipeline` | `purview-dev/sourcegenerator-framework` | changing the incremental pipeline or its value-equatable models |
| `purview-sourcegenerator-diagnostics` | `purview-dev/sourcegenerator-framework` | adding or changing a `VO1xxx` diagnostic or the analyzer |
| `purview-sourcegenerator-packaging` | `purview-dev/sourcegenerator-framework` | changing how the generator is packed or merged |
| `purview-sourcegenerator-testing` | `purview-dev/sourcegenerator-framework` | writing generator, analyzer or code-fix tests (`CodeQuery`) |
| `purview-sourcegenerator-tunit-tests` | `purview-dev/sourcegenerator-framework` | the TUnit base classes and assertion extensions |
| `purview-zodsharp-*` (6 skills) | `purview-dev/zodsharp` | the `[ZodSchema]` integration, custom rules, JSON, ASP.NET Core and `ZODSGEN` diagnostics |

Skills are installed into the universal `.agents/skills/` location **only**. Never install copies into `.claude/`
or any other per-agent folder, and never add a `CLAUDE.md`: `AGENTS.md` is the one instruction file, and
`/.claude/` is gitignored. Pass `-a universal`, which writes `.agents/skills/` and nothing else:

```bash
# add or update every skill from a source
npx -y skills@1 add purview-dev/build-sdk -s '*' -a universal -y
npx -y skills@1 add purview-dev/sourcegenerator-framework -s '*' -a universal -y
npx -y skills@1 add purview-dev/zodsharp -s '*' -a universal -y

# restore the committed set from skills-lock.json
npx -y skills@1 experimental_install
```

Commit the changed `.agents/skills/**` and `skills-lock.json` together, in their own `chore` commit. Do not edit
anything under `.agents/skills/` by hand - the CLI is its only writer, and a hand edit no longer matches the
lockfile's hash. `Purview.BuildSdk`'s `InstallPurviewSkills` target delegates to the same CLI;
`src/Directory.Build.props` sets `PurviewSkillsAgents=universal` so it is restricted the same way rather than
defaulting to every agent (`*`).

`src/tests/Repository.UnitTests/InstalledSkillTests.cs` keeps this honest: every `skills-lock.json` entry has its
`.agents/skills/<name>/SKILL.md`, every installed folder is in the lockfile, no entry is one of this repository's
own skills, and neither a `.claude/skills/` copy nor a `CLAUDE.md` exists.

## Agent skills

Authored skills live in the repository-root `skills/` folder. Nothing packs them: `Purview.BuildSdk`
delivers skills through the skills CLI, which clones the *git* repository and only discovers a root
`skills/` folder.

`.agents/skills/` holds the skills installed from **other** repositories (above). This repository's own skills are
never installed into it — that would leave two copies to drift apart. Read them at their authored path,
`skills/<name>/SKILL.md`.

Consumers install these skills with:

```bash
npx skills add purview-dev/value-objects
```

| Skill | Answers |
| -- | -- |
| `skills/purview-value-objects/` | modelling a value object: scalar, complex, contextual, `Create`/`Hydrate`, normalization, typed identifiers |
| `skills/purview-value-objects-efcore/` | persisting one with EF Core: converters, complex-type vs JSON, keys, generated key values, migrations |
| `skills/purview-value-objects-zodsharp/` | validating one with ZodSharp: `[ZodSchema]`, `ZodSchemaMode`, `OnZodValidate`, custom rules, Problem Details |
| `skills/purview-value-objects-diagnostics/` | what a `VO1xxx` means and how to fix it |

Every skill carries the `purview-value-objects` prefix. Skills install into one flat directory per
agent, shared with every skill a developer has installed from anywhere, so an unprefixed name
collides and the loser is whichever installed second.

`src/tests/Repository.UnitTests/SkillTests.cs` validates them on every test run, through
`Purview.Skills.Testing`'s rule set (the Agent Skills specification: frontmatter fields, name and
description constraints, bundled links, body size). The `[InheritsTests]` attribute on that class is
load-bearing — TUnit does not walk base types during discovery, so without it the run is green and
nothing is checked. That project pins `net10.0` rather than taking the repository's `All` set,
because it asserts something about the repository rather than about the runtime's reach.

A relative link from one `SKILL.md` into another skill's folder is a rule violation: only a skill's
own folder is installed with it, so such a link works here and nowhere else. Name the other skill in
prose instead.

