using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Command;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Application.Portfolios.Interfaces;
using Contracts.MafAgent;
using Domain.Common.Exceptions;
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
/// Plan T023: one chat turn. It only ever PROPOSES: the turn's pending actions come back as DTOs built from the
/// staged instances, and nothing is executed. A failed turn returns an error, so the unit of work commits nothing.
/// </summary>
public class SendMafAgentMessageCommandHandlerTests
{
    private const long UserId = 7L;
    private const string Session = "{\"turns\":1}";
    private const string UpdatedSession = "{\"turns\":2}";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly IMafConversationRepository _conversations = Substitute.For<IMafConversationRepository>();
    private readonly IMafAgentRunner _runner = Substitute.For<IMafAgentRunner>();
    private readonly IPortfolioRepository _portfolios = Substitute.For<IPortfolioRepository>();
    private readonly ListLogger<SendMafAgentMessageCommandHandler> _logger = new();
    private readonly PendingActionFactory _factory;
    private readonly MafConversation _conversation;

    public SendMafAgentMessageCommandHandlerTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(c => "enc:" + c.Arg<string>());
        _encryption.Decrypt(Arg.Any<string>()).Returns(c => c.Arg<string>()["enc:".Length..]);
        _currentUser.UserId.Returns(UserId);
        _factory = new PendingActionFactory(_encryption, _currentUser, _time);

        _conversation = MafConversation.Create(UserId, "enc:" + Session, Now);
        _conversations.GetByIdAsync(_conversation.Id, Arg.Any<CancellationToken>()).Returns(_conversation);
        RunnerReturns([]);
        _time.Advance(TimeSpan.FromMinutes(5));
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void RunnerReturns(IReadOnlyList<PendingAction> proposed) =>
        _runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActivePortfolio?>(), Arg.Any<CancellationToken>())
            .Returns(new MafAgentTurnResult("Hecho.", UpdatedSession, proposed));

    private Task<ErrorOr<SendMafAgentMessageResponse>> Send(Guid? conversationId, string message = "hola", long? portfolioId = null) =>
        new SendMafAgentMessageCommandHandler(_conversations, _runner, _portfolios, _factory, _encryption, _currentUser, _time, _logger)
            .Handle(new SendMafAgentMessageCommand(new SendMafAgentMessageRequest(conversationId, message, portfolioId)), Token);

    private Portfolio PortfolioOf(long userId, long id, string name)
    {
        Portfolio portfolio = Portfolio.Create(userId, name, "");
        typeof(Portfolio).BaseType!.GetProperty(nameof(Portfolio.Id))!.SetValue(portfolio, id);
        _portfolios.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(portfolio);
        return portfolio;
    }

    private Task RunnerNotCalled() =>
        _runner.DidNotReceiveWithAnyArgs().RunTurnAsync(default!, default!, default!, default, Token);

    // ---- new conversation ----

    [Fact]
    public async Task NoConversationId_StartsANewConversation_WithAnEmptySession_AndStagesItAfterTheTurn()
    {
        MafConversation? passed = null;
        _runner.RunTurnAsync(Arg.Do<MafConversation>(c => passed = c), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActivePortfolio?>(), Arg.Any<CancellationToken>())
            .Returns(new MafAgentTurnResult("Hecho.", UpdatedSession, []));

        ErrorOr<SendMafAgentMessageResponse> result = await Send(null);

        result.IsError.Should().BeFalse();
        passed.Should().NotBeNull();
        passed!.UserId.Should().Be(UserId);
        await _runner.Received(1).RunTurnAsync(passed, "", "hola", null, Token);
        _conversations.Received(1).Add(passed);
        _conversations.DidNotReceiveWithAnyArgs().Update(default!);
        passed.EncryptedSessionState.Should().Be("enc:" + UpdatedSession);
        passed.LastActivityAt.Should().Be(Now);
        result.Value.ConversationId.Should().Be(passed.Id);
        result.Value.Reply.Should().Be("Hecho.");
        await _conversations.DidNotReceiveWithAnyArgs().GetByIdAsync(default, Token);
    }

    [Fact]
    public async Task NewConversation_WhoseTurnFails_IsNeverStaged()
    {
        _runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActivePortfolio?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AgentTimeoutException());

        ErrorOr<SendMafAgentMessageResponse> result = await Send(null);

        result.IsError.Should().BeTrue();
        _conversations.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // ---- existing conversation ----

    [Fact]
    public async Task ExistingConversation_RunsOnTheDecryptedSession_AndStoresTheNewOneEncrypted()
    {
        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id, "¿cuánto vale?");

        result.IsError.Should().BeFalse();
        await _runner.Received(1).RunTurnAsync(_conversation, Session, "¿cuánto vale?", null, Token);
        _conversation.EncryptedSessionState.Should().Be("enc:" + UpdatedSession);
        _conversation.LastActivityAt.Should().Be(Now);
        _conversations.Received(1).Update(_conversation);
        _conversations.DidNotReceiveWithAnyArgs().Add(default!);
        result.Value.ConversationId.Should().Be(_conversation.Id);
    }

    [Fact]
    public async Task UnknownConversation_IsNotFound_WithoutRunningTheAgent()
    {
        Guid unknown = Guid.NewGuid();

        ErrorOr<SendMafAgentMessageResponse> result = await Send(unknown);

        result.FirstError.Code.Should().Be("MafAgent.Conversation.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        await RunnerNotCalled();
    }

    [Fact]
    public async Task AnotherUsersConversation_IsTheSameNotFound_WithoutRevealingIt()
    {
        _currentUser.UserId.Returns(8L);

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id);

        result.FirstError.Code.Should().Be("MafAgent.Conversation.NotFound");
        await RunnerNotCalled();
        _encryption.DidNotReceiveWithAnyArgs().Decrypt(default!);
    }

    [Fact]
    public async Task ConversationIdleForMoreThan24Hours_IsExpired_WithoutRunningTheAgent()
    {
        _conversation.Touch(Now - MafConversation.InactivityTimeout - TimeSpan.FromTicks(1));

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id);

        result.FirstError.Code.Should().Be("MafAgent.Conversation.Expired");
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        await RunnerNotCalled();
    }

    [Fact]
    public async Task ConversationIdleForExactly24Hours_IsStillUsable()
    {
        _conversation.Touch(Now - MafConversation.InactivityTimeout);

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task CorruptSession_Throws_SoNothingIsCommitted()
    {
        _encryption.Decrypt(Arg.Any<string>()).Throws(new FormatException("bad ciphertext"));

        Func<Task> act = () => Send(_conversation.Id);

        await act.Should().ThrowAsync<FormatException>();
        await RunnerNotCalled();
    }

    // ---- active portfolio ----

    [Fact]
    public async Task NoPortfolioId_RunsTheTurnWithoutAnActivePortfolio()
    {
        await Send(_conversation.Id);

        await _runner.Received(1).RunTurnAsync(_conversation, Session, "hola", null, Token);
        await _portfolios.DidNotReceiveWithAnyArgs().GetByIdAsync(default, Token);
    }

    [Fact]
    public async Task OwnPortfolioId_IsPassedToTheRunner_AsTheActivePortfolio()
    {
        PortfolioOf(UserId, 41L, "Principal");

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id, "hola", 41L);

        result.IsError.Should().BeFalse();
        await _runner.Received(1).RunTurnAsync(_conversation, Session, "hola", new ActivePortfolio(41L, "Principal"), Token);
    }

    [Fact]
    public async Task UnknownPortfolioId_IsNotFound_WithoutRunningTheAgent()
    {
        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id, "hola", 99L);

        result.FirstError.Code.Should().Be("MafAgent.Portfolio.NotFound");
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        await RunnerNotCalled();
        _conversations.DidNotReceiveWithAnyArgs().Update(default!);
    }

    [Fact]
    public async Task AnotherUsersPortfolioId_IsTheSameNotFound_WithoutRunningTheAgent()
    {
        PortfolioOf(8L, 42L, "Ajeno");

        ErrorOr<SendMafAgentMessageResponse> result = await Send(null, "hola", 42L);

        result.FirstError.Code.Should().Be("MafAgent.Portfolio.NotFound");
        await RunnerNotCalled();
        _conversations.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // ---- proposed actions ----

    [Fact]
    public async Task ProposedActions_AreReturnedAsDtos_BuiltFromTheStagedInstances_OnlyThoseOfTheTurn()
    {
        PendingAction create = _factory.Create(_conversation.Id, PendingActionOperation.CreatePortfolio,
            new CreatePortfolioPayload("Largo plazo", "Holdings"), null);
        PendingAction refresh = _factory.Create(_conversation.Id, PendingActionOperation.RefreshPrices,
            new RefreshPricesPayload(3), null);
        RunnerReturns([create, refresh]);

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id);

        result.Value.PendingActions.Should().BeEquivalentTo(
            [_factory.ToDto(create), _factory.ToDto(refresh)], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task NoProposedActions_IsAnEmptyList()
    {
        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id);

        result.Value.PendingActions.Should().BeEmpty();
    }

    // ---- failed turns (AC14) ----

    [Fact]
    public async Task LoopExhausted_IsTheLoopExhaustedError_AndTheConversationIsNotUpdated()
    {
        _runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActivePortfolio?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AgentLoopExhaustedException(5));

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id, "secreto del usuario");

        result.FirstError.Code.Should().Be("MafAgent.LoopExhausted");
        result.FirstError.Type.Should().Be(ErrorType.Failure);
        _conversations.DidNotReceiveWithAnyArgs().Update(default!);
        _conversation.EncryptedSessionState.Should().Be("enc:" + Session);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
        _logger.Entries.Should().NotContain(e => e.Message.Contains("secreto"), "logs never carry message text");
    }

    [Fact]
    public async Task Timeout_IsTheTimeoutError_AndTheConversationIsNotUpdated()
    {
        _runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActivePortfolio?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AgentTimeoutException());

        ErrorOr<SendMafAgentMessageResponse> result = await Send(_conversation.Id, "secreto del usuario");

        result.FirstError.Code.Should().Be("MafAgent.Timeout");
        result.FirstError.Type.Should().Be(ErrorType.Failure);
        _conversations.DidNotReceiveWithAnyArgs().Update(default!);
        _conversation.LastActivityAt.Should().NotBe(Now);
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
        _logger.Entries.Should().NotContain(e => e.Message.Contains("secreto"), "logs never carry message text");
    }

    [Fact]
    public async Task AnyOtherRunnerException_IsNotSwallowed()
    {
        _runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ActivePortfolio?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        Func<Task> act = () => Send(_conversation.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- shape ----

    [Fact]
    public void Handler_CannotExecuteAnything()
    {
        // No path can run a write: the handler has no executor and no sender to dispatch commands with.
        Type[] dependencies = typeof(SendMafAgentMessageCommandHandler).GetConstructors()
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToArray();

        dependencies.Should().NotContain([typeof(PendingActionExecutor), typeof(ISender), typeof(IMediator)]);
    }

    [Fact]
    public void Command_GoesThroughTheUnitOfWork()
    {
        typeof(SendMafAgentMessageCommand).Should().Implement<Application.Common.Messaging.ICommand>();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Validator_AcceptsOnlyMessagesOf1To2000Characters(int length, bool valid)
    {
        var validator = new SendMafAgentMessageCommandValidator();

        validator.Validate(new SendMafAgentMessageCommand(new SendMafAgentMessageRequest(null, new string('a', length))))
            .IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_RejectsAWhitespaceOnlyMessage()
    {
        new SendMafAgentMessageCommandValidator()
            .Validate(new SendMafAgentMessageCommand(new SendMafAgentMessageRequest(null, "   ")))
            .IsValid.Should().BeFalse();
    }
}
