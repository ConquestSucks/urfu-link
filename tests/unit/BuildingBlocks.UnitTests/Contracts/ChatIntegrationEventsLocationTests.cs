using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

namespace Urfu.Link.BuildingBlocks.UnitTests.Contracts;

/// <summary>
/// Cross-service Chat integration events must live in <c>BuildingBlocks/Contracts</c> so that
/// downstream consumers (NotificationService and friends) can deserialize them with a typed
/// reference instead of duplicating the schema. This test locks in the location and prevents
/// regression where new events are accidentally added under <c>ChatService.Api/Domain/Events/</c>.
/// </summary>
public sealed class ChatIntegrationEventsLocationTests
{
    private const string ContractsChatNamespace = "Urfu.Link.BuildingBlocks.Contracts.Integration.Chat";

    public static IEnumerable<object[]> CrossServiceEventTypes => new[]
    {
        new object[] { typeof(ChatMessageEditedEvent) },
        new object[] { typeof(ChatMessageDeletedEvent) },
        new object[] { typeof(ChatReactionAddedEvent) },
        new object[] { typeof(ChatReactionRemovedEvent) },
        new object[] { typeof(ChatMessagePinnedEvent) },
        new object[] { typeof(ChatMessageUnpinnedEvent) },
        new object[] { typeof(ChatThreadReplyPostedEvent) },
        new object[] { typeof(ChatThreadSubscriptionChangedEvent) },
    };

    [Theory]
    [MemberData(nameof(CrossServiceEventTypes))]
    public void Lives_in_buildingblocks_contracts_namespace(Type eventType)
    {
        eventType.Namespace.Should().Be(ContractsChatNamespace);
    }

    [Theory]
    [MemberData(nameof(CrossServiceEventTypes))]
    public void Implements_integration_event_interface(Type eventType)
    {
        typeof(IIntegrationEvent).IsAssignableFrom(eventType).Should().BeTrue();
    }

    [Fact]
    public void Delete_mode_enum_lives_in_contracts_namespace()
    {
        typeof(DeleteMode).Namespace.Should().Be(ContractsChatNamespace);
    }

    [Fact]
    public void Thread_subscription_reason_enum_lives_in_contracts_namespace()
    {
        typeof(ThreadSubscriptionReason).Namespace.Should().Be(ContractsChatNamespace);
    }
}
