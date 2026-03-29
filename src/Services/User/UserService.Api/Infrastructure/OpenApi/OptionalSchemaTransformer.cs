using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using UserService.Api.Application.Contracts;

namespace UserService.Api.Infrastructure.OpenApi;

/// <summary>
/// Replaces Optional&lt;T&gt; schemas with a nullable version of the inner type,
/// matching PATCH partial-update semantics (absent = no-op, null = clear).
/// </summary>
public sealed class OptionalSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly Dictionary<Type, (JsonSchemaType SchemaType, string? Format)> TypeMap = new()
    {
        [typeof(string)]         = (JsonSchemaType.String,  null),
        [typeof(int)]            = (JsonSchemaType.Integer, "int32"),
        [typeof(long)]           = (JsonSchemaType.Integer, "int64"),
        [typeof(double)]         = (JsonSchemaType.Number,  "double"),
        [typeof(float)]          = (JsonSchemaType.Number,  "float"),
        [typeof(bool)]           = (JsonSchemaType.Boolean, null),
        [typeof(Guid)]           = (JsonSchemaType.String,  "uuid"),
        [typeof(DateTime)]       = (JsonSchemaType.String,  "date-time"),
        [typeof(DateTimeOffset)] = (JsonSchemaType.String,  "date-time"),
    };

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        var type = context.JsonTypeInfo.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Optional<>))
            return Task.CompletedTask;

        var innerType = Nullable.GetUnderlyingType(type.GetGenericArguments()[0])
                        ?? type.GetGenericArguments()[0];

        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AllOf?.Clear();

        if (TypeMap.TryGetValue(innerType, out var mapping))
        {
            // OpenAPI 3.1: nullable = String | Null
            schema.Type   = mapping.SchemaType | JsonSchemaType.Null;
            schema.Format = mapping.Format;
        }
        else
        {
            schema.Type = JsonSchemaType.Null;
        }

        return Task.CompletedTask;
    }
}
