# Changelog

All notable changes to this repository are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). `package.json` is the authoritative version.

Released versions correspond to `v<version>` GitHub releases. Entries below the `Unreleased` heading have not
been published to NuGet. No stable release has been cut yet; the published line is `1.0.0-prerelease.N`.

## Unreleased

### Added

- Improved ZodSharp integration, including updated generation of Zod rule attributes.
- MIT `LICENSE.md`.
- [Diagnostics](docs/Diagnostics.md): a consolidated reference for all 17 `VO1xxx` rules. `VO1002`–`VO1008`
  previously appeared only in the analyzer release table, and the Entity Framework and ZodSharp rules were split
  across two guides.
- **Automatic scalar declaration.** `[Scalar<TValue>]` and `[Scalar(typeof(TValue))]` let the generator
  declare the underlying property (`public TValue Value { get; init; }`); `[Scalar]` remains the manual form
  where the author declares it. The forms are mutually exclusive: declaring the generator-owned property is
  reported as the new error `VO1022`. `Nullable = true` (reference types) declares a nullable reference
  scalar; nullable value types are written directly (`[Scalar<int?>]` / `[Scalar(typeof(int?))]`). A nullable
  reference type cannot be a generic attribute argument or a `typeof` operand (`CS8970`/`CS8639`).
- **Nullable scalars round-trip JSON `null`.** The generated converter and `ScalarJsonConverterFactory`
  accept `null` for a nullable scalar (a `T?` reference or value type) and still reject it for a non-nullable
  reference scalar.
- **ZodSharp integration for the automatic forms.** With `Purview.ZodSharp 2.0.2`, `[Scalar<T>]` /
  `[Scalar(typeof(T))]` combined with `[ZodSchema]` generate a `{Type}Schema` from the attribute (the
  underlying type and property name) and validate through `Create`, even though the property is not visible
  to the ZodSharp generator. ZodSharp 2.0.2 also ships `[NullOrNonWhiteSpace]` for the nullable
  "null or non-whitespace" rule.
- **Scalar formatting.** The generated value object now mirrors the underlying value's formatting overloads
  per type. When the wrapped type implements `System.IFormattable` (for example `decimal`, `Guid`,
  `DateTime`, or an enum), the value object implements `IFormattable` and forwards
  `ToString(string? format, IFormatProvider? formatProvider)`. When the wrapped type declares a format-only
  `ToString(string? format)` (for example `Guid`, `DateTime`, or an enum), that overload is forwarded too.
  The value object exposes the same formatting options as the property it wraps, so
  `amount.ToString("N2", CultureInfo.InvariantCulture)`, `id.ToString("N")`, and
  `string.Format("{0:N2}", amount)` behave as they would on the underlying value. A scalar whose underlying
  type declares neither overload (`string`, `bool`) is unchanged, a null underlying value formats as the
  empty string, and an author-declared overload is left untouched.
- **Scalar interface mirroring.** The generated value object now implements the same standard interfaces as
  the type it wraps, so it behaves like that type in equality, formatting, and parsing contexts:
  - `IEquatable<TValue>` when the underlying type implements it, and `IEquatable<TSelf>` for non-record
    classes (previously only non-record structs and records implemented it).
  - `ISpanFormattable` / `IUtf8SpanFormattable` when the underlying type implements them, forwarding
    `TryFormat` (a null underlying value writes nothing and reports success).
  - `IParsable<TSelf>` / `ISpanParsable<TSelf>` / `IUtf8SpanParsable<TSelf>` when the underlying type
    implements them; `Parse` validates through `Create`, and `TryParse` returns `false` (through `TryCreate`)
    for input that does not parse or fails domain validation.

  Detection is per type and per target framework, so a scalar only gets the interfaces its underlying type
  actually implements; an interface is skipped when the author already declares one of its members. Numeric
  and arithmetic interfaces, `IConvertible`, and collection interfaces are intentionally not mirrored.

### Fixed

- **The Entity Framework registry mapped members the author had excluded, and broke the model outright for
  a computed one.** `ConfigureValueObjects` walked every readable property of every entity type and
  configured it with `modelBuilder.Entity(t).Property(type, name)` — *explicit* configuration, which
  outranks `[NotMapped]` and `Ignore(...)`. Consequences, all now fixed and covered by Entity Framework
  integration tests:
  - A property marked `[NotMapped]` was pulled back into the model, producing a column and a migration
    nobody asked for — against a production schema.
  - A property excluded with `modelBuilder.Entity<T>().Ignore(...)` was likewise re-added.
  - A **computed, setter-less** property made the entire `DbContext` fail to build:
    `InvalidOperationException: No backing field could be found for property '...' and the property does
    not have a setter.` With `[NotMapped]` also being overridden there was no way to opt out, so an entity
    with a computed value-object property could not be used at all.

  The registry now skips a property that is `[NotMapped]`, ignored on the entity type, or computed and
  setter-less. A get-only **auto**-property is still mapped — it has a compiler-generated backing field,
  which Entity Framework Core maps — so the check is for that field rather than merely for a missing
  setter. `[NotMapped]` and `.Ignore(` previously appeared nowhere in the test suite.
- **A nullable value type scalar emitted a `CS8607` warning in its generated `GetHashCode`.** The generated
  override suppressed the BCL `[DisallowNull]` annotation on `EqualityComparer<T>.GetHashCode` only for a
  nullable *reference* scalar; a nullable *value* type scalar (`[Scalar<int?>]`) now suppresses it too — both
  hash `null` as 0 at runtime — so the generated code is warning-free.
- **A strict-mode deserialization failure returned a 500 instead of a 400.** With
  `ValueObjectDeserializationMode.Strict`, the validation exception from `Create` — `ArgumentException` by
  default, `ZodException` with ZodSharp validation — escaped `JsonSerializer` unwrapped. ASP.NET Core treats
  that as an unhandled exception, so a caller sending a bad value got a 500 and a stack trace rather than a
  validation response, with no indication of which member failed. It is now wrapped in `JsonException`, so
  System.Text.Json attaches `Path` and `LineNumber` and hosts treat it as bad input. The original exception
  is the inner one.
- **Strict mode could silently fall back to `Hydrate`, skipping validation entirely.** If no
  `Create(TScalar)` factory was resolvable, the converter quietly used the replay-safe factory instead —
  a validation boundary reporting success while doing nothing, which is the worst possible failure mode for
  the one mode chosen specifically to re-validate untrusted input. It now throws with an explanatory
  message. Falling back the other way (Hydrate → Create) is still allowed, because that only ever adds
  validation. Note this path is not reachable through the generator, which always emits `Create`; the fix
  hardens it for hand-written scalars.
- **A `string` scalar's ordering disagreed with its own equality, and varied by machine.** Equality and
  `GetHashCode` are generated with `EqualityComparer<string>.Default` (ordinal), but `CompareTo` used
  `Comparer<string>.Default`, which orders by the **current culture**. Two consequences:
  - `CompareTo(other) == 0` no longer implied `Equals(other)`, breaking the `IComparable<T>` contract. A
    `SortedSet`, `SortedDictionary` or `List.BinarySearch` — all of which use `CompareTo` for identity —
    therefore disagreed with the type's own notion of equality.
  - The same `OrderBy` over the same data produced different results under a different thread culture, and
    different results again from the database's collation.

  A `string` scalar now compares with `StringComparer.Ordinal`, which agrees with equality and is
  deterministic. The relational operators and the strongly typed `CompareTo` both delegate to it, so they
  are fixed too. A get-only auto-property and non-string scalars are unaffected.
- **`TryCreate` threw instead of returning `false` for a ZodSharp-validated value object.** It caught only
  `ArgumentException`, but a `[ZodSchema]` `Create` throws `ZodException` — so the documented "try"
  contract did not hold for a headline feature. It now also catches `ZodException`, emitted only when Zod
  validation is generated. Note a custom `OnValidate` that throws something else still propagates; throw
  `ArgumentException` from it to signal a validation failure.
- **`[Scalar("CustomName")]` did not compile.** The generated partial always implements
  `IScalarValueObject<TSelf, TValue>`, which declares a member named `Value`, but with a custom property name
  no `Value` was emitted — so the build failed with `CS0535` *inside generated code the consumer cannot
  edit*. `ScalarAttribute.PropertyName` is a documented public option. The generator now emits a `Value`
  member forwarding to the custom-named one, so the author's chosen name stays the primary accessor while the
  advertised interface contract is honoured. Covered by a **compiling** regression test; the previous
  coverage was an incremental-cache test that never compiled its output, which is why this was missed.
- The scalar ZodSharp adapter.
- **Nullable reference scalars now compile cleanly.** The Entity Framework converter emitted
  `typeof(string?)` (`CS8639`), `is` patterns used a nullable reference type (`CS8116`), the primitive
  `CompareTo`/`Equals` overload was ambiguous for a nullable value type, and `GetHashCode`/`ToString`
  produced nullable warnings. The generated code now strips the annotation where it is not representable and
  handles null in those members.

### Changed — trimming and Native AOT

- `Purview.ValueObjects` is now marked `IsAotCompatible`, which enables `IsTrimmable` and both the trim and
  AOT analyzers. The assembly is clean under both, so a future regression is a build warning here rather than
  a runtime failure in a consumer's published application.
- `ScalarJsonConverterFactory` — the one deliberately reflection-based component — now declares its
  requirement with `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` on its **constructor**, so a host
  registering it in a trimmed or AOT build is warned at the opt-in site instead of failing at runtime. The
  attributes cannot go on the `CreateConverter` override: `JsonConverterFactory.CreateConverter` is not
  annotated and IL2046/IL3051 require them to match the base.
- Worth stating plainly: **you usually do not need that factory.** A generated scalar already carries
  `[JsonConverter(typeof(<Type>JsonConverter))]` pointing at a generated, reflection-free converter, and that
  path is trim- and AOT-safe. Register the factory only for a hand-written scalar, in a host that is neither
  trimmed nor AOT-compiled.
- `AGENTS.md` now records trimming and Native AOT as a **non-negotiable**: the generated path stays
  reflection-free and an `IsAotCompatible` warning is a defect. The nullable-scalar support adds no reflection
  to the generated converter; the opt-in `ScalarJsonConverterFactory` remains the only reflection component.

### Changed

- Removed `[assembly: InternalsVisibleTo("SourceGenerator.UnitTests")]` from the runtime assembly. It granted
  nothing — the assembly declares no internal members — and it leaked a test-assembly name into shipped public
  metadata. This also aligns with the repository rule that cross-component contracts must be public, because the
  `Purview.SourceGeneratorFramework` merge pass strips `InternalsVisibleTo`.

### Documentation

- Recorded `VO1011`, `VO1012`, `VO1014` and `VO1020` as retired identifiers that are never reused, so the
  discontinuous numbering is intentional and documented.
- Fixed `mkdocs.yml`'s `edit_uri`, which pointed at the deleted `ef-integration` branch and so broke every
  "Edit this page" link on the published site.
- Fixed the `Copyright` property in `src/Directory.Build.props`: the `©` had been replaced by U+FFFD in an
  encoding round-trip and was shipping in package metadata.

### Changed — analyzer release tracking

- **All nine remaining diagnostics moved from `AnalyzerReleases.Unshipped.md` into
  `AnalyzerReleases.Shipped.md`** under the existing `## Release 1.0.0` heading, so the catalogue records
  all 16 rules as shipping in the first stable release. The heading was already there for `VO1001`–`VO1008`
  even though no 1.0.0 was ever tagged, so merging into it is what makes the file accurate.
- **`VO1005` is retired rather than implemented.** The catalogue listed it as a shipped `Error` and the
  documentation told you to add a constructor taking the scalar's underlying type, but it was absent from the
  analyzer's `SupportedDiagnostics` and had no reporting site anywhere — it was declared in the initial commit
  and never wired up, so it has never fired in any version. Implementing it would have been wrong: a missing
  constructor is not an error condition. When a `[Scalar]` type declares no constructor matching its scalar
  value, `ScalarValueObjectEmitter.EmitConstructor` **emits a private one**, which is the documented and
  tested behaviour. The rule as written contradicted the generator. The id now sits with `VO1011`, `VO1012`,
  `VO1014` and `VO1020` as retired and never reused, leaving 17 live rules.
- **`VO1022` ships in a new `## Release 1.0.1` block** in `AnalyzerReleases.Shipped.md`, for the automatic
  scalar forms that declare the underlying property themselves.

### Versioning and package lineage

- `Purview.ZodSharp` continues the [`guinhx/ZodSharp`](https://github.com/guinhx/ZodSharp) project (its `v1`
  line). The package was moved to `Purview.*` and restarted at `v2` so existing `v1` consumers can migrate;
  `Purview.ValueObjects` integrates with `Purview.ZodSharp 2.0.2`.

## 1.0.0-prerelease.11

See the
[`v1.0.0-prerelease.11`](https://github.com/purview-dev/value-objects/releases/tag/v1.0.0-prerelease.11)
release notes. Earlier prereleases `1` through `10` are listed under
[releases](https://github.com/purview-dev/value-objects/releases).
