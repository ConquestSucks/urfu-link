using System.Text.Json;
using System.Text.Json.Serialization;

namespace UserService.Api.Application.Contracts;

/// <summary>
/// Represents a value that may or may not have been provided in a PATCH request.
/// Absent field → HasValue=false (no change). Explicit null → HasValue=true, Value=null (clear).
/// </summary>
public readonly struct Optional<T> : IEquatable<Optional<T>>
{
    internal Optional(T? value)
    {
        HasValue = true;
        Value = value;
    }

    public bool HasValue { get; }
    public T? Value { get; }

    public bool Equals(Optional<T> other)
        => HasValue == other.HasValue && EqualityComparer<T>.Default.Equals(Value, other.Value);

    public override bool Equals(object? obj)
        => obj is Optional<T> other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(HasValue, Value);

    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);
    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);
}

/// <summary>Factory methods for <see cref="Optional{T}"/>.</summary>
public static class Optional
{
    public static Optional<T> Set<T>(T? value) => new(value);
}

public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.IsGenericType
               && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        var innerType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalJsonConverter<>).MakeGenericType(innerType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.Null
            ? default
            : JsonSerializer.Deserialize<T>(ref reader, options);

        return Optional.Set(value);
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.HasValue)
            JsonSerializer.Serialize(writer, value.Value, options);
        else
            writer.WriteNullValue();
    }
}
