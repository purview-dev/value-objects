using System.ComponentModel;
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
