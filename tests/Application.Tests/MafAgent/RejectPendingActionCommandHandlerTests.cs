using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Command;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Application.Tests.MafAgent;

/// <summary>
/// Plan T021: rejecting is the atomic Pending → Rejected transition of <c>TryRejectAsync</c>, never an
/// update of the loaded entity, so a reject and a confirm can never both win. Nothing is ever sent.
/// </summary>
public class RejectPendingActionCommandHandlerTests
{
    private const long UserId = 7L;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly IPendingActionRepository _actions = Substitute.For<IPendingActionRepository>();
    private readonly IMafConversationRepository _conversations = Substitute.For<IMafConversationRepository>();
    private readonly PendingActionFactory _factory;
    private readonly MafConversation _conversation;

    public RejectPendingActionCommandHandlerTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(c => "enc:" + c.Arg<string>());
        _currentUser.UserId.Returns(UserId);
        _factory = new PendingActionFactory(_encryption, _currentUser, _time);

        _conversation = MafConversation.Create(UserId, "enc:{}", Now);
        _conversations.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>()).Returns(_conversation);
        _actions.TryRejectAsync(Arg.Any<Guid>(), UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private Task<ErrorOr<Success>> Reject(Guid id) =>
        new RejectPendingActionCommandHandler(_actions, _conversations, _currentUser, _time)
            .Handle(new RejectPendingActionCommand(id), TestContext.Current.CancellationToken);

    private PendingAction Stored()
    {
        PendingAction action = _factory.Create(
            _conversation.Id, PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Largo plazo", "Holdings"), null);
        _actions.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);
        return action;
    }

    private void Decide(PendingAction action, PendingActionStatus status)
    {
        if (status == PendingActionStatus.Rejected)
        {
            action.MarkRejected(Now);
            return;
        }

        action.MarkExecuting();
        switch (status)
        {
            case PendingActionStatus.Succeeded: action.MarkSucceeded("enc:{}", Now); break;
            case PendingActionStatus.Failed: action.MarkFailed("Some.Code", Now); break;
            case PendingActionStatus.Stale: action.MarkStale(Now); break;
        }
    }

    private Task NotRejectedAtomically() =>
        _actions.DidNotReceiveWithAnyArgs().TryRejectAsync(default, default, default, TestContext.Current.CancellationToken);

    [Fact]
    public async Task PendingAction_IsRejectedThroughTheAtomicTransition_NotThroughTheLoadedEntity()
    {
        PendingAction action = Stored();

        ErrorOr<Success> result = await Reject(action.Id);

        result.IsError.Should().BeFalse();
        await _actions.Received(1).TryRejectAsync(action.Id, UserId, Now, TestContext.Current.CancellationToken);
        _actions.DidNotReceiveWithAnyArgs().Update(default!);
        action.Status.Should().Be(PendingActionStatus.Pending, "the loaded entity is never mutated, so the unit of work cannot overwrite a concurrent confirm");
        action.DecidedAt.Should().BeNull();
    }

    [Fact]
    public async Task LostRace_TryRejectFalse_IsAlreadyDecided()
    {
        PendingAction action = Stored();
        _actions.TryRejectAsync(action.Id, UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(false);

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.AlreadyDecided");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task UnknownId_IsNotFound()
    {
        ErrorOr<Success> result = await Reject(Guid.NewGuid());

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        await NotRejectedAtomically();
    }

    [Fact]
    public async Task AnotherUsersAction_IsTheSameNotFound_AndIsNotRejected()
    {
        PendingAction action = Stored();
        _currentUser.UserId.Returns(8L);

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        await NotRejectedAtomically();
    }

    [Fact]
    public async Task ActionOwnedByAnotherUser_InsideTheCallersConversation_IsStillNotFound()
    {
        // Defence in depth: the action's own owner is checked, not only its conversation's.
        _currentUser.UserId.Returns(8L);
        PendingAction foreign = Stored();
        _currentUser.UserId.Returns(UserId);

        ErrorOr<Success> result = await Reject(foreign.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        await NotRejectedAtomically();
    }

    [Fact]
    public async Task CallersAction_InsideAnotherUsersConversation_IsStillNotFound()
    {
        // Defence in depth: the conversation's owner is checked too, not only the action's.
        MafConversation foreign = MafConversation.Create(8L, "enc:{}", Now);
        _conversations.GetByIdAsync(foreign.Id, Arg.Any<CancellationToken>()).Returns(foreign);
        PendingAction action = _factory.Create(
            foreign.Id, PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Largo plazo", "Holdings"), null);
        _actions.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        await NotRejectedAtomically();
    }

    [Fact]
    public async Task ActionWhoseConversationIsGone_IsNotFound()
    {
        PendingAction action = Stored();
        _conversations.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>()).Returns((MafConversation?)null);

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        await NotRejectedAtomically();
    }

    [Fact]
    public async Task ExpiredAction_IsExpired()
    {
        PendingAction action = Stored();
        _time.Advance(PendingAction.Lifetime);

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.Expired");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        await NotRejectedAtomically();
    }

    [Fact]
    public async Task ActionOfAnExpiredConversation_IsExpired()
    {
        PendingAction action = Stored();
        _conversation.Touch(Now - TimeSpan.FromHours(24) - TimeSpan.FromTicks(1));

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.Expired");
        await NotRejectedAtomically();
    }

    [Theory]
    [InlineData(PendingActionStatus.Executing)]
    [InlineData(PendingActionStatus.Succeeded)]
    [InlineData(PendingActionStatus.Failed)]
    [InlineData(PendingActionStatus.Rejected)]
    [InlineData(PendingActionStatus.Stale)]
    public async Task AlreadyDecidedAction_IsAlreadyDecided_WithoutTryingTheTransition(PendingActionStatus status)
    {
        PendingAction action = Stored();
        Decide(action, status);

        ErrorOr<Success> result = await Reject(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.AlreadyDecided");
        await NotRejectedAtomically();
    }

    [Fact]
    public void Command_DoesNotGoThroughTheUnitOfWork()
    {
        // TryRejectAsync commits on its own; nothing is staged for UnitOfWorkBehavior to flush.
        typeof(RejectPendingActionCommand).Should().NotImplement<Application.Common.Messaging.ICommand>();
    }

    [Fact]
    public void Validator_RejectsAnEmptyId()
    {
        var validator = new RejectPendingActionCommandValidator();

        validator.Validate(new RejectPendingActionCommand(Guid.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new RejectPendingActionCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
