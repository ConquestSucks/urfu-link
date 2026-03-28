using NJsonSchema;
using NJsonSchema.Generation;
using UserService.Api.Application.Contracts;

namespace UserService.Api.Infrastructure.OpenApi;

/// <summary>
/// NSwag (FastEndpoints.Swagger) schema processor that rewrites Optional&lt;T&gt; schemas
/// as nullable T instead of an object with {hasValue, value} properties.
/// </summary>
public sealed class OptionalNSwagSchemaProcessor : ISchemaProcessor
{
    private static readonly Dictionary<Type, (JsonObjectType ObjectType, string? Format)> TypeMap = new()
    {
        [typeof(string)]         = (JsonObjectType.String,  null),
        [typeof(int)]            = (JsonObjectType.Integer, "int32"),
        [typeof(long)]           = (JsonObjectType.Integer, "int64"),
        [typeof(double)]         = (JsonObjectType.Number,  "double"),
        [typeof(float)]          = (JsonObjectType.Number,  "float"),
        [typeof(bool)]           = (JsonObjectType.Boolean, null),
        [typeof(Guid)]           = (JsonObjectType.String,  "uuid"),
        [typeof(DateTime)]       = (JsonObjectType.String,  "date-time"),
        [typeof(DateTimeOffset)] = (JsonObjectType.String,  "date-time"),
    };

    public void Process(SchemaProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var clrType = context.ContextualType.OriginalType;
        if (!clrType.IsGenericType || clrType.GetGenericTypeDefinition() != typeof(Optional<>))
            return;

        var innerType = Nullable.GetUnderlyingType(clrType.GetGenericArguments()[0])
                        ?? clrType.GetGenericArguments()[0];

        var schema = context.Schema;
        schema.Properties.Clear();
        schema.AllOf.Clear();
        schema.IsNullableRaw = true;

        if (TypeMap.TryGetValue(innerType, out var mapping))
        {
            schema.Type   = mapping.ObjectType | JsonObjectType.Null;
            schema.Format = mapping.Format;
        }
        else
        {
            schema.Type = JsonObjectType.Null;
        }
    }
}
