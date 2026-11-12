using Domain.Entities;
using FluentAssertions;

namespace Domain.Tests.Entities;

/// <summary>Spec maf-tool-calling-agent AC15/AC16; plan T003.</summary>
public sealed class MafConversationTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsOwnerSessionAndTimestamps()
    {
        MafConversation conversation = MafConversation.Create(userId: 7, encryptedSessionState: "cipher", nowUtc: Now);

        conversation.Id.Should().NotBe(Guid.Empty);
        conversation.UserId.Should().Be(7);
        conversation.EncryptedSessionState.Should().Be("cipher");
        conversation.CreatedAt.Should().Be(Now);
        conversation.LastActivityAt.Should().Be(Now);
    }

    [Fact]
    public void Create_TwoConversations_HaveDifferentIds()
    {
        MafConversation first = MafConversation.Create(7, "a", Now);
        MafConversation second = MafConversation.Create(7, "b", Now);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void IsExpired_ExactlyTwentyFourHoursAfterLastActivity_IsNotExpired()
    {
        MafConversation conversation = MafConversation.Create(7, "cipher", Now);

        conversation.IsExpired(Now.AddHours(24)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_OneTickPastTwentyFourHours_IsExpired()
    {
        MafConversation conversation = MafConversation.Create(7, "cipher", Now);

        conversation.IsExpired(Now.AddHours(24).AddTicks(1)).Should().BeTrue();
    }

    [Fact]
    public void Touch_MovesLastActivityAndPostponesExpiry()
    {
        MafConversation conversation = MafConversation.Create(7, "cipher", Now);
        DateTime later = Now.AddHours(20);

        conversation.Touch(later);

        conversation.LastActivityAt.Should().Be(later);
        conversation.IsExpired(Now.AddHours(25)).Should().BeFalse();
        conversation.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void UpdateSession_ReplacesEncryptedStateAndTouches()
    {
        MafConversation conversation = MafConversation.Create(7, "old", Now);
        DateTime later = Now.AddMinutes(5);

        conversation.UpdateSession("new", later);

        conversation.EncryptedSessionState.Should().Be("new");
        conversation.LastActivityAt.Should().Be(later);
    }
}
