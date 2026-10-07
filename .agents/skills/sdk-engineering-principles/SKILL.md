---
name: sdk-engineering-principles
description: "Use when creating or rationalising a repository that uses Purview.BuildSdk and you need the policy-level conventions for project placement, naming, namespace identity, test categories, and large-suite readability."
---

# Purview.BuildSdk engineering principles

Use this skill when the question is not just "what property does the SDK set?" but "how should this
repository be structured so the SDK can work predictably?"

## First principle

Treat naming, placement, and test structure as configuration.

The SDK infers namespaces, identities, categories, package wiring, and project references from a small set
of conventions. The more a repository follows those conventions, the less it needs bespoke overrides.

## Canonical defaults

- Prefer a `src/` + `tests/` split for new repositories.
- Keep solution entry points under `src/{SolutionName}.slnx`.
- Keep source projects under `src/src/{ProjectName}/{ProjectName}.csproj`.
- Keep test projects under `src/tests/{ProjectName}.{TestType}Tests/{ProjectName}.{TestType}Tests.csproj`.
- Keep the `.csproj` filename equal to its containing directory name.

## Identity rules

- `NamespacePrefix` is the root identity source.
- Use short project names; let the SDK apply the prefix.
- `RootNamespace` is the canonical code identity by default.
- `AssemblyName` and `PackageId` usually follow the resolved project identity.
- When suffix stripping would collapse distinct artifacts, `AssemblyName` and `PackageId` keep the fuller
  logical identity.

Examples:

- `NamespacePrefix=Aspire`, project `Hosting` -> `Aspire.Hosting`
- `NamespacePrefix=Acme.Sales.RegionalPipeline`, project `Identity.API` ->
  `Acme.Sales.RegionalPipeline.Identity.API`
- `NamespacePrefix=Acme.Sales.RegionalPipeline`, project `Identity.Core` -> `RootNamespace`
  `Acme.Sales.RegionalPipeline.Identity`, but a distinct assembly/package identity that keeps `Core`

## Common test project types

Prefer these by default:

- `UnitTests`
- `IntegrationTests`
- `E2ETests`
- `FunctionalTests`
- `ContractTests`

The SDK supports more suffixes, but use them only when the test type itself is important enough to carry in
the project name.

## Test categories

- The detected test type becomes the baseline category automatically.
- Additional categories are allowed.
- Add more categories when they improve discoverability for large suites.

## Default test stack

Standard test projects receive these by default:

- `TUnit`
- `TUnit.Mocks`
- `Bogus`
- Microsoft.Testing.Platform integration

Specialized additions remain explicit:

- `TUnit.Aspire` for Aspire lifecycle/AppHost-backed integration tests
- `Testcontainers` for container-backed integration tests

## Test readability rules

For subject-based tests:

- Prefer `{SubjectName}Tests` for the class.
- Prefer `{SubjectOrMemberUnderTest}_{Scenario}_{Expectation}` for methods.
- Treat methods, constructors, properties, operators, conversions, and validation hooks as valid subjects.

For non-subject-based suites:

- Broader names are allowed when they are more truthful and readable.
- Use TUnit display names, categories, and data-driven metadata to keep the suite navigable.

## When to use this skill vs others

- Use this skill for policy, structure, naming, and repository-shape questions.
- Use `sdk-project-behavior-and-detection` for "why did the SDK classify this project this way?"
- Use `sdk-configuration-reference` for property-level questions.
- Use `project-placement-defaults` when physically creating or moving projects.
