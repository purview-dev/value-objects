using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// A <see cref="JsonConverterFactory"/> that produces JSON converters for types decorated with
/// <see cref="ScalarAttribute"/>, serializing only the scalar value.
/// </summary>
/// <remarks>
/// <para>
/// Scalar value objects are persisted as their underlying <see cref="ScalarAttribute.PropertyName"/> value
/// rather than as a full object graph. On write the converter serializes the scalar member; on read it
/// reconstructs the value object using the configured <see cref="ValueObjectDeserializationMode"/> (defaulting
/// to <see cref="ValueObjectDeserializationMode.Hydrate"/>), falling back to a public or non-public
/// constructor taking the scalar value.
/// </para>
/// <para>
/// Converters are cached per type to avoid repeated reflection and expression compilation.
/// </para>
/// <para>
/// <b>Trimming and Native AOT.</b> This factory is the one reflection-based component in
/// <c>Purview.ValueObjects</c>: it reads <see cref="ScalarAttribute"/>, looks the scalar member up by name,
/// builds a closed generic converter with <see cref="Type.MakeGenericType"/>, compiles accessors with
/// expression trees, and serializes through the reflection-based <see cref="JsonSerializer"/> overloads.
/// None of that survives trimming or Native AOT, so <see cref="CreateConverter"/> is annotated and will
/// raise IL2026/IL3050 at your call site rather than failing at runtime in production.
/// </para>
/// <para>
/// <b>You usually do not need it.</b> A generated scalar value object already carries
/// <c>[JsonConverter(typeof(&lt;Type&gt;JsonConverter))]</c>, pointing at a converter the generator emitted
/// with no reflection. That path is trim- and AOT-safe, and it is the default. Register this factory only
/// for a hand-written scalar that has no generated converter, and only in a host that is neither trimmed
/// nor AOT-compiled.
/// </para>
/// </remarks>
public sealed class ScalarJsonConverterFactory : JsonConverterFactory
{
	static readonly ConcurrentDictionary<Type, JsonConverter> Cache = new();

	/// <summary>
	/// Creates the factory.
	/// </summary>
	/// <remarks>
	/// The trimming and Native AOT requirement is declared here because this is where a host opts in, and
	/// because an override of <see cref="JsonConverterFactory.CreateConverter"/> cannot carry the attributes
	/// itself (IL2046/IL3051 require them to match the unannotated base). Registering this factory in a
	/// trimmed or AOT-compiled host will warn at the construction site instead of failing at runtime.
	/// </remarks>
	[RequiresUnreferencedCode(
		"ScalarJsonConverterFactory reads ScalarAttribute, resolves the scalar member and the "
			+ "Create/Hydrate factory by name, and serializes through reflection-based JsonSerializer "
			+ "overloads. Generated scalar value objects already carry a [JsonConverter] pointing at a "
			+ "reflection-free generated converter; prefer that and do not register this factory."
	)]
	[RequiresDynamicCode(
		"ScalarJsonConverterFactory builds a closed generic converter with Type.MakeGenericType and compiles "
			+ "accessors with expression trees. Prefer the generated per-type JsonConverter."
	)]
	public ScalarJsonConverterFactory() { }

	/// <summary>
	/// Determines whether the type can be converted, returning true when it is decorated with
	/// <see cref="ScalarAttribute"/>.
	/// </summary>
	/// <param name="typeToConvert">The type being checked.</param>
	/// <returns>True when the type is a scalar value object, otherwise false.</returns>
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.GetCustomAttribute<ScalarAttribute>() is not null;

	/// <summary>
	/// Creates a converter for the given scalar value object type.
	/// </summary>
	/// <param name="typeToConvert">The scalar value object type.</param>
	/// <param name="options">The serializer options the converter will be used with.</param>
	/// <returns>A cached <see cref="JsonConverter"/> instance for the type.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the type does not expose the configured scalar property, or no static
	/// <c>Create</c>/<c>Hydrate</c> factory or scalar constructor can be found.
	/// </exception>
	/// <remarks>
	/// Not supported under trimming or Native AOT; prefer the generated per-type converter. See the remarks
	/// on <see cref="ScalarJsonConverterFactory"/>. The requirement is declared on the constructor rather
	/// than here, because <see cref="JsonConverterFactory.CreateConverter"/> is not itself annotated and
	/// IL2046/IL3051 forbid an override from adding the attribute.
	/// </remarks>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:RequiresDynamicCode",
		Justification = "Only reachable through the annotated ScalarJsonConverterFactory constructor."
	)]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "Only reachable through the annotated ScalarJsonConverterFactory constructor."
	)]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:UnrecognizedReflectionPattern",
		Justification = "Only reachable through the annotated ScalarJsonConverterFactory constructor."
	)]
	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		Cache.GetOrAdd(
			typeToConvert,
			static t =>
			{
				var attr = t.GetCustomAttribute<ScalarAttribute>()!;
				var scalarProp =
					t.GetProperty(attr.PropertyName, BindingFlags.Instance | BindingFlags.Public)
					?? throw new InvalidOperationException(
						$"'{t.Name}' missing scalar property '{attr.PropertyName}'."
					);

				var converterType = typeof(ScalarJsonConverter<,>).MakeGenericType(t, scalarProp.PropertyType);
				return (JsonConverter)Activator.CreateInstance(converterType, scalarProp, attr.DeserializationMode)!;
			}
		);

	// Instances are only ever created by CreateConverter, which is itself only reachable through the
	// annotated constructor, so the requirement is already declared to the consumer at the opt-in point.
	// These suppressions stop it being reported a second time against code the consumer cannot change.
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:RequiresUnreferencedCode",
		Justification = "Only constructed through the annotated ScalarJsonConverterFactory constructor."
	)]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2090:UnrecognizedReflectionPattern",
		Justification = "Only constructed through the annotated ScalarJsonConverterFactory constructor."
	)]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:RequiresDynamicCode",
		Justification = "Only constructed through the annotated ScalarJsonConverterFactory constructor."
	)]
	sealed class ScalarJsonConverter<TScalarObject, TScalar>(
		PropertyInfo scalarProperty,
		ValueObjectDeserializationMode deserializationMode
	) : JsonConverter<TScalarObject>
	{
		readonly Func<TScalarObject, TScalar> _getScalar = BuildGetter(scalarProperty);
		readonly Func<TScalar, TScalarObject> _create = BuildCreator(deserializationMode);

		public override TScalarObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var scalar = JsonSerializer.Deserialize<TScalar>(ref reader, options);
			if (scalar is null)
				throw new JsonException($"Cannot deserialize {typeof(TScalarObject).Name} from null.");

			try
			{
				return _create(scalar);
			}
			catch (Exception exception) when (IsValidationFailure(exception))
			{
				// Surfaced as JsonException so System.Text.Json attaches Path and LineNumber, and so hosts
				// treat it as bad input. Under Strict the factory is Create, which throws the validation
				// exception of whatever validator the value object uses - ArgumentException by default, or
				// ZodException with ZodSharp validation. Letting those escape unwrapped meant ASP.NET Core
				// saw an unhandled exception and answered 500 instead of 400, with no indication of which
				// property failed.
				throw new JsonException(
					$"Cannot deserialize {typeof(TScalarObject).Name}: the value failed validation.",
					exception
				);
			}
		}

		/// <summary>
		/// Whether <paramref name="exception"/> is a validation failure from the value object's factory
		/// rather than something that must not be reported as malformed input.
		/// </summary>
		static bool IsValidationFailure(Exception exception) =>
			exception
				is not (OperationCanceledException or OutOfMemoryException or StackOverflowException or JsonException);

		public override void Write(Utf8JsonWriter writer, TScalarObject value, JsonSerializerOptions options) =>
			JsonSerializer.Serialize(writer, _getScalar(value), options);

		static Func<TScalarObject, TScalar> BuildGetter(PropertyInfo property)
		{
			var obj = Expression.Parameter(typeof(TScalarObject), "x");
			var body = Expression.Property(obj, property);
			return Expression.Lambda<Func<TScalarObject, TScalar>>(body, obj).Compile();
		}

		static Func<TScalar, TScalarObject> BuildCreator(ValueObjectDeserializationMode deserializationMode)
		{
			var t = typeof(TScalarObject);
			var isStrict = deserializationMode == ValueObjectDeserializationMode.Strict;
			var preferredFactoryName = isStrict ? "Create" : "Hydrate";

			var create = t.GetMethod(
				preferredFactoryName,
				BindingFlags.Public | BindingFlags.Static,
				[typeof(TScalar)]
			);

			// Strict must never silently become Hydrate. Strict is chosen precisely to re-validate untrusted
			// input, so falling back to the replay-safe factory would report success while skipping
			// validation altogether - a validation boundary that does nothing. Hydrate may still fall back to
			// Create, which only ever adds validation.
			if (create is null && isStrict)
			{
				throw new InvalidOperationException(
					$"'{t.Name}' is configured with {nameof(ValueObjectDeserializationMode)}."
						+ $"{nameof(ValueObjectDeserializationMode.Strict)}, but no public static "
						+ $"'Create({typeof(TScalar).Name})' factory could be found. Strict deserialization "
						+ "re-validates the incoming value, so it cannot fall back to Hydrate."
				);
			}

			create ??= t.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, [typeof(TScalar)]);

			if (create is not null)
			{
				var p = Expression.Parameter(typeof(TScalar), "v");
				return Expression.Lambda<Func<TScalar, TScalarObject>>(Expression.Call(create, p), p).Compile();
			}

			// Fallback: ctor(TScalar) (public or non-public)
			var ctor = t.GetConstructor(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				[typeof(TScalar)]
			);
			if (ctor is not null)
			{
				var p = Expression.Parameter(typeof(TScalar), "v");
				return Expression.Lambda<Func<TScalar, TScalarObject>>(Expression.New(ctor, p), p).Compile();
			}

			throw new InvalidOperationException(
				$"{t.Name} must expose static Hydrate({typeof(TScalar).Name}), static "
					+ $"Create({typeof(TScalar).Name}), or ctor({typeof(TScalar).Name})."
			);
		}
	}
}
