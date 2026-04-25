using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

/// <summary>
/// One-time global Bson configuration. Registers GUID Standard representation and a camelCase
/// + ignore-extra-elements convention pack for the chat namespace.
/// </summary>
internal static class BsonRegistrations
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String),
        };
        ConventionRegistry.Register(
            "chat-conventions",
            pack,
            type => type.FullName?.StartsWith("Urfu.Link.Services.Chat", StringComparison.Ordinal) == true);
    }
}
