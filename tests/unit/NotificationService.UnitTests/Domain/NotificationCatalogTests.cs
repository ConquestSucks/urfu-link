using FluentAssertions;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Domain;

public sealed class NotificationCatalogTests
{
    [Theory]
    [InlineData(NotificationCategory.ChatMessageDirect, "chat.message.direct")]
    [InlineData(NotificationCategory.ChatMessageMention, "chat.mention")]
    [InlineData(NotificationCategory.ChatMessageDiscipline, "chat.message.discipline")]
    [InlineData(NotificationCategory.CallMissed, "call.missed")]
    [InlineData(NotificationCategory.DisciplineDeadline, "discipline.deadline")]
    [InlineData(NotificationCategory.SystemMaintenance, "system.maintenance")]
    public void GetByCategory_ReturnsStableFrontendType(NotificationCategory category, string expectedType)
    {
        var descriptor = NotificationCatalog.GetByCategory(category);

        descriptor.Type.Should().Be(expectedType);
        descriptor.Category.Should().Be(category);
        descriptor.Icon.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryGet_ReturnsFalseForUnknownType()
    {
        NotificationCatalog.TryGet("unknown.type", out _).Should().BeFalse();
    }
}
