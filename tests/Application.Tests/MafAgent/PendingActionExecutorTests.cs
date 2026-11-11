using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Application.Notes.Command;
using Application.PortfolioEntries.Command;
using Application.Portfolios.Command;
using Application.Reports.Command;
using Contracts.Notes;
using Contracts.PortfolioEntries;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Application.Tests.MafAgent;

/// <summary>Plan T017: confirming runs exactly the existing command with the payload values (AC3, AC10, AC10b).</summary>
public class PendingActionExecutorTests
{
    private static readonly DateTime RecordedAt = new(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IAddedEntityIdLog _addedIds = Substitute.For<IAddedEntityIdLog>();
    private readonly PendingActionFactory _factory;

    public PendingActionExecutorTests()
    {
        IEncryptionService encryption = Substitute.For<IEncryptionService>();
        encryption.Encrypt(Arg.Any<string>()).Returns(c => c.Arg<string>());
        encryption.Decrypt(Arg.Any<string>()).Returns(c => c.Arg<string>());
        ICurrentUserProvider currentUser = Substitute.For<ICurrentUserProvider>();
        currentUser.UserId.Returns(7L);
        _factory = new PendingActionFactory(encryption, currentUser, new FakeTimeProvider(new DateTimeOffset(Now)));
    }

    private PendingActionExecutor CreateExecutor() => new(_sender, _addedIds, _factory);

    private PendingAction Action(PendingActionOperation op, object payload, IReadOnlyDictionary<string, string?>? before = null) =>
        _factory.Create(Guid.NewGuid(), op, payload, before);

    private static IReadOnlyDictionary<string, string?> BeforeOf(object payload) => FieldSnapshot.OfRequestFields(payload);

    [Fact]
    public async Task CreatePortfolio_DispatchesCreatePortfolioCommand_AndReturnsTheIdFromTheAddedEntityLog()
    {
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<CreatePortfolioResponse>>(new CreatePortfolioResponse(0, "Largo plazo")));
        _addedIds.LastAddedId<Portfolio>().Returns(42L);

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Largo plazo", "Holdings")), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CreatePortfolioCommand>(c => c.Request.Name == "Largo plazo" && c.Request.Description == "Holdings"),
            Arg.Any<CancellationToken>());
        result.Value.Should().BeEquivalentTo(new CreatePortfolioResponse(42, "Largo plazo"));
    }

    [Fact]
    public async Task UpdatePortfolio_DispatchesUpdatePortfolioCommand_WithTheTargetIdAndTheFullRequest()
    {
        var response = new UpdatePortfolioResponse(3, 7, "Nuevo", "Desc", Now, Now);
        _sender.Send(Arg.Any<UpdatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<UpdatePortfolioResponse>>(response));
        var payload = new UpdatePortfolioPayload(3, "Nuevo", "Desc");

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.UpdatePortfolio, payload, BeforeOf(payload with { Name = "Viejo" })), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<UpdatePortfolioCommand>(c => c.Id == 3 && c.Request.Name == "Nuevo" && c.Request.Description == "Desc"),
            Arg.Any<CancellationToken>());
        result.Value.Should().Be(response);
    }

    [Fact]
    public async Task CreateEntry_DispatchesEveryField_AndReturnsTheIdFromTheAddedEntityLog()
    {
        _sender.Send(Arg.Any<CreatePortfolioEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<CreatePortfolioEntryResponse>>(
                new CreatePortfolioEntryResponse(0, 3, 20, 30, 0.5m, 60000m, RecordedAt)));
        _addedIds.LastAddedId<PortfolioEntry>().Returns(55L);

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.CreateEntry, new CreateEntryPayload(3, 20, 30, 0.5m, 60000m, RecordedAt)), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CreatePortfolioEntryCommand>(c =>
                c.Request.PortfolioId == 3 && c.Request.CryptoCurrencyId == 20 && c.Request.ExchangeId == 30 &&
                c.Request.Quantity == 0.5m && c.Request.PricePerUnit == 60000m &&
                c.Request.RecordedAt == RecordedAt && c.Request.RecordedAt.Kind == DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
        result.Value.Should().BeOfType<CreatePortfolioEntryResponse>().Which.Id.Should().Be(55);
    }

    [Fact]
    public async Task UpdateEntry_DispatchesUpdatePortfolioEntryCommand_WithTheTargetIdAndEveryField()
    {
        var response = new UpdatePortfolioEntryResponse(9, 3, 20, 30, 0.75m, 60000m, RecordedAt);
        _sender.Send(Arg.Any<UpdatePortfolioEntryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<UpdatePortfolioEntryResponse>>(response));
        var payload = new UpdateEntryPayload(9, 20, 30, 0.75m, 60000m, RecordedAt);

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.UpdateEntry, payload, BeforeOf(payload with { Quantity = 0.5m })), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<UpdatePortfolioEntryCommand>(c =>
                c.Id == 9 && c.Request.CryptoCurrencyId == 20 && c.Request.ExchangeId == 30 &&
                c.Request.Quantity == 0.75m && c.Request.PricePerUnit == 60000m && c.Request.RecordedAt == RecordedAt),
            Arg.Any<CancellationToken>());
        result.Value.Should().Be(response);
    }

    [Fact]
    public async Task CreateNote_DispatchesCreateNoteCommand_AsAnActiveNote()
    {
        var response = new CreateNoteResponse("note-1", "Ideas", "Rebalanceo", "Vender ETH", Now, true);
        _sender.Send(Arg.Any<CreateNoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<CreateNoteResponse>>(response));

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.CreateNote, new CreateNotePayload("Ideas", "Rebalanceo", "Vender ETH")), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<CreateNoteCommand>(c =>
                c.Request.Category == "Ideas" && c.Request.Title == "Rebalanceo" && c.Request.Content == "Vender ETH" &&
                c.Request.IsActive && c.Request.CreatedAt == null),
            Arg.Any<CancellationToken>());
        result.Value.Should().Be(response);
    }

    [Fact]
    public async Task UpdateNote_OfAnInactiveNote_DispatchesIsActiveFalse()
    {
        _sender.Send(Arg.Any<UpdateNoteCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<UpdateNoteResponse>>(new UpdateNoteResponse("note-1", "Ideas", "Nuevo", "x", Now, false)));
        var payload = new UpdateNotePayload("note-1", "Ideas", "Nuevo", "x", false);

        await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.UpdateNote, payload, BeforeOf(payload with { Title = "Viejo" })), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<UpdateNoteCommand>(c =>
                c.Id == "note-1" && c.Request.Category == "Ideas" && c.Request.Title == "Nuevo" &&
                c.Request.Content == "x" && !c.Request.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshPrices_ReturnsBothTheUpdatedAndTheSkippedHoldings()
    {
        var response = new RefreshPortfolioPricesResponse(
            3, Now,
            [new RefreshedHoldingItem(20, "BTC", 30, 0.5m, 60000m, 61000m)],
            [new SkippedHoldingItem(21, "XYZ", "NoCoinGeckoIdMapped")]);
        _sender.Send(Arg.Any<RefreshPortfolioPricesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<RefreshPortfolioPricesResponse>>(response));

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.RefreshPrices, new RefreshPricesPayload(3)), CancellationToken.None);

        await _sender.Received(1).Send(Arg.Is<RefreshPortfolioPricesCommand>(c => c.Id == 3), Arg.Any<CancellationToken>());
        RefreshPortfolioPricesResponse refreshed = result.Value.Should().BeOfType<RefreshPortfolioPricesResponse>().Subject;
        refreshed.Updated.Should().ContainSingle(u => u.Symbol == "BTC");
        refreshed.Skipped.Should().ContainSingle(s => s.Symbol == "XYZ");
    }

    [Fact]
    public async Task SendStatusReport_DispatchesTheTriggerWithTheConcretePeriod_AndReportsItAsSent()
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
        _sender.Send(Arg.Any<TriggerPortfolioStatusReportCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<Success>>(Result.Success));

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.SendStatusReport, new SendStatusReportPayload(3, start, end)), CancellationToken.None);

        await _sender.Received(1).Send(
            Arg.Is<TriggerPortfolioStatusReportCommand>(c => c.PortfolioId == 3 && c.PeriodStart == start && c.PeriodEnd == end),
            Arg.Any<CancellationToken>());
        result.Value.Should().Be(new StatusReportSentResult(3, start, end));
    }

    [Fact]
    public async Task Dispatch_AlwaysUsesCancellationTokenNone_EvenIfTheCallerPassesALiveToken()
    {
        _sender.Send(Arg.Any<RefreshPortfolioPricesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<RefreshPortfolioPricesResponse>>(new RefreshPortfolioPricesResponse(3, Now, [], [])));
        using var live = new CancellationTokenSource();

        await CreateExecutor().ExecuteAsync(Action(PendingActionOperation.RefreshPrices, new RefreshPricesPayload(3)), live.Token);

        await _sender.Received(1).Send(Arg.Any<RefreshPortfolioPricesCommand>(), CancellationToken.None);
    }

    [Fact]
    public async Task ACommandError_IsReturnedAsAnError_WithoutReadingAnAddedId()
    {
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<CreatePortfolioResponse>>(Error.Conflict("Portfolio.AlreadyExists", "dup")));

        ErrorOr<object> result = await CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Largo plazo", "x")), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Portfolio.AlreadyExists");
        _addedIds.DidNotReceive().LastAddedId<Portfolio>();
    }

    [Fact]
    public async Task ACreateThatSucceededWithoutARecordedId_IsAWiringError()
    {
        _sender.Send(Arg.Any<CreatePortfolioCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<CreatePortfolioResponse>>(new CreatePortfolioResponse(0, "Largo plazo")));
        _addedIds.LastAddedId<Portfolio>().Returns((long?)null);

        Func<Task> execute = () => CreateExecutor().ExecuteAsync(
            Action(PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Largo plazo", "x")), CancellationToken.None);

        await execute.Should().ThrowAsync<InvalidOperationException>();
    }
}
