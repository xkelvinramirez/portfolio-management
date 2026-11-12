using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Command;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Application.Portfolios.Command;
using Contracts.MafAgent;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Application.Tests.MafAgent;

/// <summary>
/// Plan T019: confirming by id is the only way a proposed write runs. Errors only BEFORE the claim; after a
/// successful claim every outcome (Succeeded, Failed, Stale) is a value, so the unit of work persists it.
/// </summary>
public class ConfirmPendingActionCommandHandlerTests
{
    private const long UserId = 7L;
    private const string Session = "{\"turns\":1}";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly IPendingActionRepository _actions = Substitute.For<IPendingActionRepository>();
    private readonly IMafConversationRepository _conversations = Substitute.For<IMafConversationRepository>();
    private readonly ICurrentValuesReader _reader = Substitute.For<ICurrentValuesReader>();
    private readonly ITrackedChangesDiscarder _discarder = Substitute.For<ITrackedChangesDiscarder>();
    private readonly IMafAgentRunner _runner = Substitute.For<IMafAgentRunner>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IAddedEntityIdLog _addedIds = Substitute.For<IAddedEntityIdLog>();
    private readonly ListLogger<ConfirmPendingActionCommandHandler> _logger = new();
    private readonly PendingActionFactory _factory;
    private readonly MafConversation _conversation;

    public ConfirmPendingActionCommandHandlerTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(c => "enc:" + c.Arg<string>());
        _encryption.Decrypt(Arg.Any<string>()).Returns(c => c.Arg<string>()["enc:".Length..]);
        _currentUser.UserId.Returns(UserId);
        _factory = new PendingActionFactory(_encryption, _currentUser, _time);

        _conversation = MafConversation.Create(UserId, "enc:" + Session, Now);
        _conversations.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>()).Returns(_conversation);
        _actions.TryClaimAsync(Arg.Any<Guid>(), UserId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        _runner.AppendSystemNoteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<string>(0) + "+note");
        _addedIds.LastAddedId<Portfolio>().Returns(42L);
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private ConfirmPendingActionCommandHandler CreateHandler() => new(
        _actions,
        _conversations,
        _reader,
        new PendingActionExecutor(_sender, _addedIds, _factory),
        _factory,
        _discarder,
        _runner,
        _encryption,
        _currentUser,
        _time,
        _logger);

    private Task<ErrorOr<PendingActionResultDto>> Confirm(Guid id) =>
        CreateHandler().Handle(new ConfirmPendingActionCommand(id), TestContext.Current.CancellationToken);

    private PendingAction Stored(PendingActionOperation op, object payload, IReadOnlyDictionary<string, string?>? before = null)
    {
        PendingAction action = _factory.Create(_conversation.Id, op, payload, before);
        _actions.GetByIdAsync(action.Id, Arg.Any<CancellationToken>()).Returns(action);
        return action;
    }

    private PendingAction StoredCreatePortfolio() =>
        Stored(PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Largo plazo", "Holdings"));

    private void CreatePortfolioReturns(ErrorOr<CreatePortfolioResponse> result) =>
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(result));

    private PendingAction StoredPortfolioEdit()
    {
        var payload = new UpdatePortfolioPayload(3, "Nuevo", "Desc");
        IReadOnlyDictionary<string, string?> before = FieldSnapshot.OfRequestFields(payload with { Name = "Viejo" });
        return Stored(PendingActionOperation.UpdatePortfolio, payload, before);
    }

    // ---- before the claim: errors ----

    [Fact]
    public async Task UnknownId_IsNotFound()
    {
        ErrorOr<PendingActionResultDto> result = await Confirm(Guid.NewGuid());

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        await _actions.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnotherUsersAction_IsTheSameNotFound_AndIsNotClaimed()
    {
        PendingAction action = StoredCreatePortfolio();
        _currentUser.UserId.Returns(8L);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        await _actions.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default, default, TestContext.Current.CancellationToken);
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnExpiredAction_IsExpired()
    {
        PendingAction action = StoredCreatePortfolio();
        _time.Advance(TimeSpan.FromMinutes(10));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.Expired");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        await _actions.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnActionOfAConversationIdleForMoreThan24Hours_IsExpired()
    {
        PendingAction action = StoredCreatePortfolio();
        _conversation.Touch(Now - TimeSpan.FromHours(24) - TimeSpan.FromTicks(1));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.Expired");
        await _actions.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnAlreadyDecidedAction_IsAlreadyDecided()
    {
        PendingAction action = StoredCreatePortfolio();
        action.MarkRejected(Now);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.AlreadyDecided");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        await _actions.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ALostClaim_IsAlreadyDecided_AndNothingRuns()
    {
        PendingAction action = StoredCreatePortfolio();
        _actions.TryClaimAsync(action.Id, UserId, Now, Arg.Any<CancellationToken>()).Returns(false);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.AlreadyDecided");
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ---- after the claim: always a value ----

    [Fact]
    public async Task HappyPath_Succeeds_WithTheResult_DecidedAt_AndAnEncryptedResultJson()
    {
        PendingAction action = StoredCreatePortfolio();
        CreatePortfolioReturns(new CreatePortfolioResponse(0, "Largo plazo"));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Succeeded");
        result.Value.Operation.Should().Be("CreatePortfolio");
        result.Value.Result.Should().BeEquivalentTo(new CreatePortfolioResponse(42, "Largo plazo"));
        result.Value.Error.Should().BeNull();
        action.Status.Should().Be(PendingActionStatus.Succeeded);
        action.DecidedAt.Should().Be(Now);
        action.ResultJson.Should().StartWith("enc:").And.Contain("42");
        _encryption.Received().Encrypt(Arg.Is<string>(json => json.Contains("42") && json.Contains("Largo plazo")));
        _actions.Received(1).Update(action);
    }

    [Fact]
    public async Task TheClaimUsesTheCurrentUserAndClock()
    {
        PendingAction action = StoredCreatePortfolio();
        CreatePortfolioReturns(new CreatePortfolioResponse(0, "Largo plazo"));

        await Confirm(action.Id);

        await _actions.Received(1).TryClaimAsync(action.Id, UserId, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterTheClaim_TheActionIsExecutingWhileTheCommandRuns()
    {
        PendingAction action = StoredCreatePortfolio();
        PendingActionStatus? statusDuringCommand = null;
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                statusDuringCommand = action.Status;
                return Task.FromResult<ErrorOr<CreatePortfolioResponse>>(new CreatePortfolioResponse(0, "Largo plazo"));
            });

        await Confirm(action.Id);

        statusDuringCommand.Should().Be(PendingActionStatus.Executing);
    }

    [Fact]
    public async Task AStaleEdit_IsAValueWithStatusStale_MarkedStale_AndNothingIsDispatched()
    {
        PendingAction action = StoredPortfolioEdit();
        _reader.ReadAsync(PendingActionOperation.UpdatePortfolio, "3", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(
                new Dictionary<string, string?> { ["Name"] = "Cambiado por otra vía", ["Description"] = "Desc" }));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Stale");
        result.Value.Error!.Code.Should().Be("MafAgent.PendingAction.Stale");
        action.Status.Should().Be(PendingActionStatus.Stale);
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
        _actions.Received(1).Update(action);
    }

    [Fact]
    public async Task AnUnchangedEdit_IsDispatched()
    {
        PendingAction action = StoredPortfolioEdit();
        _reader.ReadAsync(PendingActionOperation.UpdatePortfolio, "3", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(
                new Dictionary<string, string?> { ["Name"] = "Viejo", ["Description"] = "Desc" }));
        _sender.Send(Arg.Any<UpdatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<UpdatePortfolioResponse>>(new UpdatePortfolioResponse(3, UserId, "Nuevo", "Desc", Now, Now)));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.Value.Status.Should().Be("Succeeded");
        await _sender.Received(1).Send(Arg.Is<UpdatePortfolioCommand>(c => c.Id == 3 && c.Request.Name == "Nuevo"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AResourceDeletedBetweenProposalAndConfirmation_IsAValueWithStatusFailed()
    {
        PendingAction action = StoredPortfolioEdit();
        _reader.ReadAsync(PendingActionOperation.UpdatePortfolio, "3", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(Error.NotFound("Portfolio.NotFound", "gone")));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Failed");
        result.Value.Error!.Code.Should().Be("Portfolio.NotFound");
        action.Status.Should().Be(PendingActionStatus.Failed);
        action.ErrorCode.Should().Be("Portfolio.NotFound");
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ACommandFailure_IsAValueWithStatusFailed_AndItsErrorCode()
    {
        PendingAction action = StoredCreatePortfolio();
        CreatePortfolioReturns(Error.Conflict("Portfolio.AlreadyExists", "dup"));

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Failed");
        result.Value.Error.Should().Be(new PendingActionErrorDto("Portfolio.AlreadyExists", "dup"));
        action.Status.Should().Be(PendingActionStatus.Failed);
        action.ErrorCode.Should().Be("Portfolio.AlreadyExists");
        _actions.Received(1).Update(action);
    }

    // ---- review of T017–T020 ----

    [Fact]
    public async Task AMissingConversation_IsNotFound()
    {
        PendingAction action = StoredCreatePortfolio();
        _conversations.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>()).Returns((MafConversation?)null);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.FirstError.Code.Should().Be("MafAgent.PendingAction.NotFound");
        await _actions.DidNotReceiveWithAnyArgs().TryClaimAsync(default, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ACreate_DoesNotReadCurrentValues()
    {
        PendingAction action = StoredCreatePortfolio();
        CreatePortfolioReturns(new CreatePortfolioResponse(0, "Largo plazo"));

        await Confirm(action.Id);

        await _reader.DidNotReceiveWithAnyArgs().ReadAsync(default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AStaleEdit_IsDecided_AndNotedInTheSession()
    {
        PendingAction action = StoredPortfolioEdit();
        _reader.ReadAsync(PendingActionOperation.UpdatePortfolio, "3", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(
                new Dictionary<string, string?> { ["Name"] = "Otro", ["Description"] = "Desc" }));

        await Confirm(action.Id);

        action.DecidedAt.Should().Be(Now);
        await _runner.Received(1).AppendSystemNoteAsync(Session, Arg.Is<string>(n => n.Contains("Stale")), Arg.Any<CancellationToken>());
        _conversations.Received(1).Update(_conversation);
    }

    [Fact]
    public async Task ASessionNoteFailure_AfterTheWrite_KeepsTheRealOutcome_AsAValue()
    {
        PendingAction action = StoredCreatePortfolio();
        CreatePortfolioReturns(new CreatePortfolioResponse(0, "Largo plazo"));
        var noteFailure = new System.Text.Json.JsonException("corrupt session");
        _runner.AppendSystemNoteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(noteFailure);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Succeeded");
        action.Status.Should().Be(PendingActionStatus.Succeeded);
        _actions.Received(1).Update(action);
        _discarder.DidNotReceive().DiscardAll();
        await _actions.DidNotReceiveWithAnyArgs().TryMarkFailedAsync(default, default!, default, TestContext.Current.CancellationToken);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Exception == noteFailure);
    }

    [Fact]
    public async Task AnExceptionWhoseFailedMarkFinds_NoExecutingRow_IsStillAFailedValue_AndIsLogged()
    {
        PendingAction action = StoredCreatePortfolio();
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("x"));
        _actions.TryMarkFailedAsync(action.Id, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(false);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Failed");
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("no longer Executing"));
    }

    [Fact]
    public async Task AnExceptionFromTheCommand_DiscardsTrackedChangesBeforeMarkingFailed_AndIsAValue()
    {
        PendingAction action = StoredCreatePortfolio();
        var boom = new InvalidOperationException("SaveChanges failed");
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>()).ThrowsAsync(boom);
        _actions.TryMarkFailedAsync(action.Id, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Failed");
        result.Value.Error!.Code.Should().Be("MafAgent.PendingAction.UnexpectedFailure");
        Received.InOrder(() =>
        {
            _discarder.DiscardAll();
            _actions.TryMarkFailedAsync(action.Id, "MafAgent.PendingAction.UnexpectedFailure", Now, CancellationToken.None);
        });
        await _runner.DidNotReceiveWithAnyArgs().AppendSystemNoteAsync(default!, default!, TestContext.Current.CancellationToken);
        _actions.DidNotReceiveWithAnyArgs().Update(default!);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Exception == boom);
    }

    [Fact]
    public async Task AnUndecryptablePayload_AfterTheClaim_IsAValueWithStatusFailed()
    {
        PendingAction action = StoredCreatePortfolio();
        _encryption.Decrypt(action.PayloadJson).Throws(new System.Security.Cryptography.CryptographicException("bad"));
        _actions.TryMarkFailedAsync(action.Id, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        ErrorOr<PendingActionResultDto> result = await Confirm(action.Id);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be("Failed");
        _discarder.Received(1).DiscardAll();
    }

    [Fact]
    public async Task TheCommandAndTheStaleReadRunWithoutTheRequestToken()
    {
        PendingAction action = StoredPortfolioEdit();
        _reader.ReadAsync(Arg.Any<PendingActionOperation>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(
                new Dictionary<string, string?> { ["Name"] = "Viejo", ["Description"] = "Desc" }));
        _sender.Send(Arg.Any<UpdatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<UpdatePortfolioResponse>>(new UpdatePortfolioResponse(3, UserId, "Nuevo", "Desc", Now, Now)));
        using var live = new CancellationTokenSource();

        await CreateHandler().Handle(new ConfirmPendingActionCommand(action.Id), live.Token);

        await _reader.Received(1).ReadAsync(PendingActionOperation.UpdatePortfolio, "3", CancellationToken.None);
        await _sender.Received(1).Send(Arg.Any<UpdatePortfolioCommand>(), CancellationToken.None);
    }

    [Fact]
    public async Task AnActionStaysConfirmableAfterAnotherIsProposedInTheSameConversation()
    {
        PendingAction first = StoredCreatePortfolio();
        Stored(PendingActionOperation.RefreshPrices, new RefreshPricesPayload(3));
        CreatePortfolioReturns(new CreatePortfolioResponse(0, "Largo plazo"));

        ErrorOr<PendingActionResultDto> result = await Confirm(first.Id);

        result.Value.Status.Should().Be("Succeeded");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheOutcomeIsNotedInTheConversationSession_WithoutFreeText(bool succeeds)
    {
        PendingAction action = StoredCreatePortfolio();
        CreatePortfolioReturns(succeeds
            ? new CreatePortfolioResponse(0, "Largo plazo")
            : Error.Conflict("Portfolio.AlreadyExists", "dup"));

        await Confirm(action.Id);

        await _runner.Received(1).AppendSystemNoteAsync(
            Session,
            Arg.Is<string>(note => note.Contains(action.Id.ToString()) && !note.Contains("Largo plazo") && !note.Contains("Holdings")),
            Arg.Any<CancellationToken>());
        _conversation.EncryptedSessionState.Should().Be("enc:" + Session + "+note");
        _conversations.Received(1).Update(_conversation);
    }
}
