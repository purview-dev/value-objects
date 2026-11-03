using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Purview.ValueObjects.Serialization;

/// <summary>
/// Trim- and Native AOT-safe JSON plumbing for the converters the value object generator emits. Generated
/// <c>&lt;Type&gt;JsonConverter</c> classes read and write their underlying values and members through these
/// helpers rather than through the reflection-based <see cref="JsonSerializer"/> overloads.
/// </summary>
/// <remarks>
/// <para>
/// A value of type <c>T</c> in the helpers below is resolved in this order:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///       The converter the generator supplies for a member that is itself a value object, unless the options
///       register a converter for that type. A source-generated <see cref="JsonSerializerContext"/> in the
///       project that declares the value object cannot see the <c>[JsonConverter]</c> attribute the value object
///       generator adds (one source generator never sees another's output), so the context would otherwise
///       describe the value object as a plain object with a <c>Value</c> property.
///     </description>
///   </item>
///   <item>
///     <description>
///       The <see cref="JsonTypeInfo{T}"/> the caller's <see cref="JsonSerializerOptions"/> provide - the
///       reflection resolver in an ordinary application, or a source-generated <see cref="JsonSerializerContext"/>.
///       This honours every converter, naming policy and number-handling option exactly as
///       <c>JsonSerializer.Deserialize&lt;T&gt;(ref reader, options)</c> would.
///     </description>
///   </item>
///   <item>
///     <description>
///       The built-in System.Text.Json converter for a primitive (<see cref="string"/>, <see cref="Guid"/>, the
///       numeric types, <see cref="bool"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>,
///       <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="TimeSpan"/>, <see cref="Uri"/>,
///       <see cref="Version"/> and their nullable forms) when the options cannot describe it - typically a
///       source-generated context that never listed the primitive because it only saw the value object.
///     </description>
///   </item>
/// </list>
/// <para>
/// Anything else (an enum, or a custom underlying type) needs metadata: add
/// <c>[JsonSerializable(typeof(T))]</c> to the application's <see cref="JsonSerializerContext"/>. The
/// <see cref="NotSupportedException"/> thrown otherwise names the type.
/// </para>
/// <para>
/// These members exist for generated code. They are public so a consumer's generated converters can call them,
/// and hidden from IntelliSense because application code has no reason to.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ValueObjectJson
{
	/// <summary>Reads a value of type <typeparamref name="T"/> from <paramref name="reader"/>.</summary>
	/// <typeparam name="T">The type to read.</typeparam>
	/// <param name="reader">The reader, positioned on the value.</param>
	/// <param name="options">The options the enclosing converter was called with.</param>
	/// <param name="converter">
	/// The converter of a value-object-typed member, used unless <paramref name="options"/> register one for <typeparamref name="T"/>.
	/// </param>
	/// <returns>The value read.</returns>
	/// <exception cref="NotSupportedException">No metadata, supplied converter or built-in converter applies.</exception>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1045:Do not pass types by reference",
		Justification = "Mirrors JsonConverter<T>.Read, which receives the reader by reference."
	)]
	public static T? Read<T>(
		ref Utf8JsonReader reader,
		JsonSerializerOptions options,
		JsonConverter<T>? converter = null
	)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (!PrefersSupplied(converter, options) && TryGetTypeInfo<T>(options) is { } typeInfo)
			return JsonSerializer.Deserialize(ref reader, typeInfo);

		converter ??= BuiltInConverter<T>.Instance ?? throw CreateNotSupported(typeof(T));
		if (reader.TokenType == JsonTokenType.Null && !converter.HandleNull)
		{
			return default(T) is null
				? default
				: throw new JsonException($"The JSON value null could not be converted to {typeof(T)}.");
		}

		return converter.Read(ref reader, typeof(T), options);
	}

	/// <summary>Writes <paramref name="value"/> to <paramref name="writer"/>.</summary>
	/// <typeparam name="T">The type to write.</typeparam>
	/// <param name="writer">The writer.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="options">The options the enclosing converter was called with.</param>
	/// <param name="converter">
	/// The converter of a value-object-typed member, used unless <paramref name="options"/> register one for <typeparamref name="T"/>.
	/// </param>
	/// <exception cref="NotSupportedException">No metadata, supplied converter or built-in converter applies.</exception>
	public static void Write<T>(
		Utf8JsonWriter writer,
		T value,
		JsonSerializerOptions options,
		JsonConverter<T>? converter = null
	)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);

		if (!PrefersSupplied(converter, options) && TryGetTypeInfo<T>(options) is { } typeInfo)
		{
			JsonSerializer.Serialize(writer, value, typeInfo);
			return;
		}

		converter ??= BuiltInConverter<T>.Instance ?? throw CreateNotSupported(typeof(T));
		if (value is null && !converter.HandleNull)
		{
			writer.WriteNullValue();
			return;
		}

		converter.Write(writer, value, options);
	}

	/// <summary>
	/// Serializes <paramref name="value"/> to a JSON string through <typeparamref name="TConverter"/>: the trim- and
	/// Native AOT-safe equivalent of <c>JsonSerializer.Serialize(value)</c> for a type whose
	/// <c>[JsonConverter]</c> is <typeparamref name="TConverter"/>. The generated Entity Framework Core JSON-column
	/// converters use it.
	/// </summary>
	/// <remarks>
	/// While reflection-based serialization is enabled the options are <see cref="JsonSerializerOptions.Default"/>,
	/// so the output matches <c>JsonSerializer.Serialize(value)</c> exactly. Trimmed and Native AOT applications turn
	/// it off, and then a member is written through its value object's converter or the built-in converter for a
	/// primitive; any other member type (an enum, for example) throws <see cref="NotSupportedException"/>.
	/// </remarks>
	/// <typeparam name="T">The type to write.</typeparam>
	/// <typeparam name="TConverter">The converter that writes <typeparamref name="T"/>.</typeparam>
	/// <param name="value">The value to write.</param>
	/// <returns>The JSON text.</returns>
	public static string Serialize<T, TConverter>(T value)
		where TConverter : JsonConverter<T>, new()
	{
		ArrayBufferWriter<byte> buffer = new();
		using (Utf8JsonWriter writer = new(buffer))
			Write(writer, value, StringOptions.Instance, ConverterCache<T, TConverter>.Instance);

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}

	/// <summary>
	/// Deserializes <paramref name="json"/> through <typeparamref name="TConverter"/>: the trim- and Native AOT-safe
	/// equivalent of <c>JsonSerializer.Deserialize&lt;T&gt;(json)</c> for a type whose <c>[JsonConverter]</c> is
	/// <typeparamref name="TConverter"/>. The generated Entity Framework Core JSON-column converters use it.
	/// </summary>
	/// <remarks>The options are chosen as for <see cref="Serialize{T, TConverter}"/>.</remarks>
	/// <typeparam name="T">The type to read.</typeparam>
	/// <typeparam name="TConverter">The converter that reads <typeparamref name="T"/>.</typeparam>
	/// <param name="json">The JSON text.</param>
	/// <returns>The value read.</returns>
	/// <exception cref="JsonException"><paramref name="json"/> is not a single JSON value.</exception>
	public static T? Deserialize<T, TConverter>(string json)
		where TConverter : JsonConverter<T>, new()
	{
		ArgumentNullException.ThrowIfNull(json);

		Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json));
		if (!reader.Read())
			throw new JsonException($"The JSON value could not be converted to {typeof(T)}: the input is empty.");

		var value = Read(ref reader, StringOptions.Instance, ConverterCache<T, TConverter>.Instance);

		// The reader rejects a second top-level value itself; this only guards a converter that stopped short.
		if (reader.Read())
			throw new JsonException($"The JSON value could not be converted to {typeof(T)}: unexpected trailing data.");

		return value;
	}

	/// <summary>
	/// Writes a member of a complex value object: its name (with the options' naming policy applied) and its value,
	/// honouring <see cref="JsonSerializerOptions.DefaultIgnoreCondition"/>.
	/// </summary>
	/// <typeparam name="T">The member type.</typeparam>
	/// <param name="writer">The writer, positioned inside an object.</param>
	/// <param name="memberName">The member's CLR name.</param>
	/// <param name="value">The member's value.</param>
	/// <param name="options">The options the enclosing converter was called with.</param>
	/// <param name="converter">An optional converter for the member, as for <see cref="Write{T}"/>.</param>
	public static void WriteMember<T>(
		Utf8JsonWriter writer,
		string memberName,
		T value,
		JsonSerializerOptions options,
		JsonConverter<T>? converter = null
	)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(options);

		var ignore = options.DefaultIgnoreCondition;
		if (
			(ignore == JsonIgnoreCondition.WhenWritingNull && value is null)
			|| (ignore == JsonIgnoreCondition.WhenWritingDefault && EqualityComparer<T>.Default.Equals(value, default))
		)
		{
			return;
		}

		writer.WritePropertyName(GetMemberName(memberName, options));
		Write(writer, value, options, converter);
	}

	/// <summary>
	/// The JSON name of a complex value object member: <paramref name="memberName"/> with the options'
	/// <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> applied.
	/// </summary>
	/// <param name="memberName">The member's CLR name.</param>
	/// <param name="options">The options the enclosing converter was called with.</param>
	/// <returns>The name written to, and expected in, JSON.</returns>
	public static string GetMemberName(string memberName, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.PropertyNamingPolicy?.ConvertName(memberName) ?? memberName;
	}

	/// <summary>
	/// True when a JSON property name read by a complex value object converter names
	/// <paramref name="memberName"/>, honouring the naming policy and
	/// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/>.
	/// </summary>
	/// <param name="jsonName">The property name read from JSON.</param>
	/// <param name="memberName">The member's CLR name.</param>
	/// <param name="options">The options the enclosing converter was called with.</param>
	/// <returns><see langword="true"/> when the names match.</returns>
	public static bool IsMember(string? jsonName, string memberName, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return string.Equals(
			jsonName,
			GetMemberName(memberName, options),
			options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
		);
	}

	/// <summary>
	/// Whether <paramref name="exception"/>, thrown by a value object's factory during strict deserialization, is a
	/// validation failure - and so is reported as <see cref="ValidationFailed"/> - rather than something that must
	/// not be reported as bad input.
	/// </summary>
	/// <param name="exception">The exception the factory threw.</param>
	/// <returns><see langword="true"/> for a validation failure.</returns>
	public static bool IsValidationFailure(Exception exception) =>
		exception
			is not (OperationCanceledException or OutOfMemoryException or StackOverflowException or JsonException);

	/// <summary>
	/// The exception a strict converter throws when the value it read fails validation: a <see cref="JsonException"/>
	/// whose <see cref="Exception.InnerException"/> is the validator's exception (<c>ArgumentException</c> by default,
	/// <c>ZodException</c> with ZodSharp validation).
	/// </summary>
	/// <remarks>
	/// System.Text.Json fills in <see cref="JsonException.Path"/> (<c>$.customerEmail</c>) as the exception leaves the
	/// converter, which is the only way the member that held the value can be known; Purview.ZodSharp reads it to
	/// report a <c>ZodException</c>'s errors under that member (<c>CustomerEmail.Value</c>). Hosts treat a
	/// <see cref="JsonException"/> as bad input, so ASP.NET Core answers it with <c>400</c> rather than <c>500</c>.
	/// </remarks>
	/// <param name="valueObjectType">The value object being read.</param>
	/// <param name="innerException">The validation failure.</param>
	/// <returns>The exception to throw.</returns>
	public static JsonException ValidationFailed(Type valueObjectType, Exception innerException)
	{
		ArgumentNullException.ThrowIfNull(valueObjectType);

		return new($"Cannot deserialize {valueObjectType.Name}: the value failed validation.", innerException);
	}

	/// <summary>Adapts a converter for <typeparamref name="T"/> to <see cref="Nullable{T}"/>.</summary>
	/// <typeparam name="T">The underlying value type.</typeparam>
	/// <param name="converter">The converter for the underlying type.</param>
	/// <returns>A converter that reads and writes JSON <c>null</c> as <see langword="null"/>.</returns>
	public static JsonConverter<T?> Nullable<T>(JsonConverter<T> converter)
		where T : struct
	{
		ArgumentNullException.ThrowIfNull(converter);

		return new NullableConverter<T>(converter);
	}

	static bool PrefersSupplied<T>(JsonConverter<T>? converter, JsonSerializerOptions options)
	{
		if (converter is null)
			return false;

		// A converter the application registered for the value object wins, exactly as it would for a member the
		// serializer resolved itself.
		foreach (var registered in options.Converters)
		{
			if (registered.CanConvert(typeof(T)))
				return false;
		}

		return true;
	}

	static JsonTypeInfo<T>? TryGetTypeInfo<T>(JsonSerializerOptions options)
	{
		// Options with no resolver resolve through reflection only while it is enabled; once trimming or Native AOT
		// turns it off, asking would throw, so those options can only use the supplied or built-in converters.
		if (options.TypeInfoResolver is null && !JsonSerializer.IsReflectionEnabledByDefault)
			return null;

		return options.TryGetTypeInfo(typeof(T), out var typeInfo) ? typeInfo as JsonTypeInfo<T> : null;
	}

	static NotSupportedException CreateNotSupported(Type type) =>
		new(
			$"No JSON metadata is available for '{type}'. A value object converter reads and writes it through the "
				+ "JsonSerializerOptions it is called with; when those come from a source-generated JsonSerializerContext, "
				+ $"add [JsonSerializable(typeof({type}))] to that context."
		);

	static class ConverterCache<T, TConverter>
		where TConverter : JsonConverter<T>, new()
	{
		public static readonly TConverter Instance = new();
	}

	/// <summary>The options <see cref="Serialize{T, TConverter}"/> and <see cref="Deserialize{T, TConverter}"/> use.</summary>
	static class StringOptions
	{
		public static readonly JsonSerializerOptions Instance = Create();

		// JsonSerializerOptions.Default resolves metadata through reflection. It is only read while reflection-based
		// serialization is enabled, which trimmed and Native AOT applications turn off by default
		// (JsonSerializerIsReflectionEnabledByDefault), and the trimmer then removes the branch - the same rule
		// TryGetTypeInfo follows for the options a converter is called with.
		[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
			"Trimming",
			"IL2026:RequiresUnreferencedCode",
			Justification = "Only reached while JsonSerializer.IsReflectionEnabledByDefault is true."
		)]
		[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
			"AOT",
			"IL3050:RequiresDynamicCode",
			Justification = "Only reached while JsonSerializer.IsReflectionEnabledByDefault is true."
		)]
		static JsonSerializerOptions Create()
		{
			if (JsonSerializer.IsReflectionEnabledByDefault)
				return JsonSerializerOptions.Default;

			JsonSerializerOptions options = new() { TypeInfoResolver = JsonTypeInfoResolver.Combine() };
			options.MakeReadOnly();
			return options;
		}
	}

	sealed class NullableConverter<T>(JsonConverter<T> converter) : JsonConverter<T?>
		where T : struct
	{
		public override bool HandleNull => true;

		public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			reader.TokenType == JsonTokenType.Null ? null : converter.Read(ref reader, typeof(T), options);

		public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
		{
			if (value is { } inner)
				converter.Write(writer, inner, options);
			else
				writer.WriteNullValue();
		}
	}

	/// <summary>
	/// The built-in System.Text.Json converter for a primitive, or <see langword="null"/>. Each test is against a
	/// constant <see cref="Type"/>, so the JIT and ILC fold the chain to the one branch that applies.
	/// </summary>
	static class BuiltInConverter<T>
	{
		public static readonly JsonConverter<T>? Instance = (JsonConverter<T>?)Create();

		static object? Create()
		{
			if (typeof(T) == typeof(string))
				return JsonMetadataServices.StringConverter;
			if (typeof(T) == typeof(Uri))
				return JsonMetadataServices.UriConverter;
			if (typeof(T) == typeof(Version))
				return JsonMetadataServices.VersionConverter;
			if (typeof(T) == typeof(byte[]))
				return JsonMetadataServices.ByteArrayConverter;

			return CreateNumber() ?? CreateOther();
		}

		static object? CreateNumber() =>
			CreateValueType<byte>(JsonMetadataServices.ByteConverter)
			?? CreateValueType<sbyte>(JsonMetadataServices.SByteConverter)
			?? CreateValueType<char>(JsonMetadataServices.CharConverter)
			?? CreateValueType<short>(JsonMetadataServices.Int16Converter)
			?? CreateValueType<ushort>(JsonMetadataServices.UInt16Converter)
			?? CreateValueType<int>(JsonMetadataServices.Int32Converter)
			?? CreateValueType<uint>(JsonMetadataServices.UInt32Converter)
			?? CreateValueType<long>(JsonMetadataServices.Int64Converter)
			?? CreateValueType<ulong>(JsonMetadataServices.UInt64Converter)
			?? CreateValueType<Int128>(JsonMetadataServices.Int128Converter)
			?? CreateValueType<UInt128>(JsonMetadataServices.UInt128Converter)
			?? CreateValueType<float>(JsonMetadataServices.SingleConverter)
			?? CreateValueType<double>(JsonMetadataServices.DoubleConverter)
			?? CreateValueType<decimal>(JsonMetadataServices.DecimalConverter)
			?? CreateValueType<Half>(JsonMetadataServices.HalfConverter);

		static object? CreateOther() =>
			CreateValueType<bool>(JsonMetadataServices.BooleanConverter)
			?? CreateValueType<Guid>(JsonMetadataServices.GuidConverter)
			?? CreateValueType<DateTime>(JsonMetadataServices.DateTimeConverter)
			?? CreateValueType<DateTimeOffset>(JsonMetadataServices.DateTimeOffsetConverter)
			?? CreateValueType<DateOnly>(JsonMetadataServices.DateOnlyConverter)
			?? CreateValueType<TimeOnly>(JsonMetadataServices.TimeOnlyConverter)
			?? CreateValueType<TimeSpan>(JsonMetadataServices.TimeSpanConverter);

		static object? CreateValueType<TValue>(JsonConverter<TValue> converter)
			where TValue : struct
		{
			if (typeof(T) == typeof(TValue))
				return converter;

			return typeof(T) == typeof(TValue?) ? new NullableConverter<TValue>(converter) : null;
		}
	}
}
