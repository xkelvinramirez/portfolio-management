using Domain.Entities;
using FluentAssertions;

namespace Domain.Tests.Entities;

/// <summary>Spec maf-tool-calling-agent AC5/AC6/AC12; plan T005.</summary>
public sealed class PendingActionTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ConversationId = Guid.NewGuid();

    private static PendingAction NewAction(string? beforeJson = null) =>
        PendingAction.Create(ConversationId, userId: 7, PendingActionOperation.CreateEntry, "payload-cipher", beforeJson, Now);

    [Fact]
    public void Create_IsPendingAndExpiresTenMinutesLater()
    {
        PendingAction action = NewAction(beforeJson: "before-cipher");

        action.Id.Should().NotBe(Guid.Empty);
        action.ConversationId.Should().Be(ConversationId);
        action.UserId.Should().Be(7);
        action.Operation.Should().Be(PendingActionOperation.CreateEntry);
        action.PayloadJson.Should().Be("payload-cipher");
        action.BeforeJson.Should().Be("before-cipher");
        action.Status.Should().Be(PendingActionStatus.Pending);
        action.CreatedAt.Should().Be(Now);
        action.ExpiresAt.Should().Be(Now.AddMinutes(10));
        action.DecidedAt.Should().BeNull();
        action.ResultJson.Should().BeNull();
        action.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void IsExpired_JustBeforeTenMinutes_IsNotExpired()
    {
        NewAction().IsExpired(Now.AddMinutes(10).AddTicks(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AtTenMinutes_IsExpired()
    {
        NewAction().IsExpired(Now.AddMinutes(10)).Should().BeTrue();
    }

    [Fact]
    public void MarkExecuting_FromPending_MovesToExecuting()
    {
        PendingAction action = NewAction();

        action.MarkExecuting();

        action.Status.Should().Be(PendingActionStatus.Executing);
        action.DecidedAt.Should().BeNull();
    }

    [Fact]
    public void MarkSucceeded_FromExecuting_RecordsResultAndDecisionTime()
    {
        PendingAction action = NewAction();
        action.MarkExecuting();
        DateTime decidedAt = Now.AddMinutes(2);

        action.MarkSucceeded("result-cipher", decidedAt);

        action.Status.Should().Be(PendingActionStatus.Succeeded);
        action.ResultJson.Should().Be("result-cipher");
        action.DecidedAt.Should().Be(decidedAt);
        action.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_FromExecuting_RecordsErrorCodeAndDecisionTime()
    {
        PendingAction action = NewAction();
        action.MarkExecuting();
        DateTime decidedAt = Now.AddMinutes(2);

        action.MarkFailed("Portfolio.NotFound", decidedAt);

        action.Status.Should().Be(PendingActionStatus.Failed);
        action.ErrorCode.Should().Be("Portfolio.NotFound");
        action.DecidedAt.Should().Be(decidedAt);
        action.ResultJson.Should().BeNull();
    }

    [Fact]
    public void MarkStale_FromExecuting_RecordsDecisionTime()
    {
        PendingAction action = NewAction();
        action.MarkExecuting();
        DateTime decidedAt = Now.AddMinutes(2);

        action.MarkStale(decidedAt);

        action.Status.Should().Be(PendingActionStatus.Stale);
        action.DecidedAt.Should().Be(decidedAt);
    }

    [Fact]
    public void MarkRejected_FromPending_RecordsDecisionTime()
    {
        PendingAction action = NewAction();
        DateTime decidedAt = Now.AddMinutes(1);

        action.MarkRejected(decidedAt);

        action.Status.Should().Be(PendingActionStatus.Rejected);
        action.DecidedAt.Should().Be(decidedAt);
    }

    public static TheoryData<string> OutcomesThatRequireExecuting => ["Succeeded", "Failed", "Stale"];

    [Theory]
    [MemberData(nameof(OutcomesThatRequireExecuting))]
    public void Outcomes_FromPending_AreRejectedAsProgrammingErrors(string outcome)
    {
        PendingAction action = NewAction();

        Action act = () => ApplyOutcome(action, outcome);

        act.Should().Throw<InvalidOperationException>();
        action.Status.Should().Be(PendingActionStatus.Pending);
    }

    [Fact]
    public void MarkExecuting_Twice_IsRejected()
    {
        PendingAction action = NewAction();
        action.MarkExecuting();

        Action act = action.MarkExecuting;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkRejected_AfterExecuting_IsRejected()
    {
        PendingAction action = NewAction();
        action.MarkExecuting();

        Action act = () => action.MarkRejected(Now);

        act.Should().Throw<InvalidOperationException>();
        action.Status.Should().Be(PendingActionStatus.Executing);
    }

    [Fact]
    public void AnyTransition_AfterAFinalOutcome_IsRejected()
    {
        PendingAction action = NewAction();
        action.MarkExecuting();
        action.MarkSucceeded("r", Now);

        ((Action)(() => action.MarkFailed("x", Now))).Should().Throw<InvalidOperationException>();
        ((Action)(() => action.MarkStale(Now))).Should().Throw<InvalidOperationException>();
        ((Action)(() => action.MarkRejected(Now))).Should().Throw<InvalidOperationException>();
        ((Action)action.MarkExecuting).Should().Throw<InvalidOperationException>();
        action.Status.Should().Be(PendingActionStatus.Succeeded);
    }

    [Fact]
    public void Operation_CoversTheEightSupportedWrites()
    {
        Enum.GetNames<PendingActionOperation>().Should().BeEquivalentTo(
            "CreatePortfolio", "UpdatePortfolio", "CreateEntry", "UpdateEntry",
            "CreateNote", "UpdateNote", "RefreshPrices", "SendStatusReport");
    }

    private static void ApplyOutcome(PendingAction action, string outcome)
    {
        switch (outcome)
        {
            case "Succeeded": action.MarkSucceeded("r", Now); break;
            case "Failed": action.MarkFailed("x", Now); break;
            case "Stale": action.MarkStale(Now); break;
        }
    }
}
