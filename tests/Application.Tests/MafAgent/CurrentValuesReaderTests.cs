using Application.MafAgent;
using Application.MafAgent.Payloads;
using Application.Notes.Query;
using Application.PortfolioEntries.Query;
using Application.Portfolios.Query;
using Contracts.Notes;
using Contracts.PortfolioEntries;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Application.Tests.MafAgent;

/// <summary>Plan T015: the current values of an editable resource, as a snapshot comparable with Before (AC18).</summary>
public class CurrentValuesReaderTests
{
    private static readonly DateTime RecordedAt = new(2026, 11, 10, 18, 30, 0, 123, DateTimeKind.Utc);

    private readonly ISender _sender = Substitute.For<ISender>();

    private CurrentValuesReader CreateReader() => new(_sender);

    [Fact]
    public async Task UpdatePortfolio_DispatchesGetPortfolioByIdQuery_AndSnapshotsTheRequestFields()
    {
        _sender.Send(Arg.Is<GetPortfolioByIdQuery>(q => q.Id == 3), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioResponse>>(
                new PortfolioResponse(3, 7, "Largo plazo", "Holdings", RecordedAt, null)));

        ErrorOr<IReadOnlyDictionary<string, string?>> result =
            await CreateReader().ReadAsync(PendingActionOperation.UpdatePortfolio, "3", TestContext.Current.CancellationToken);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEquivalentTo(new Dictionary<string, string?> { ["Name"] = "Largo plazo", ["Description"] = "Holdings" });
    }

    [Fact]
    public async Task UpdateEntry_DispatchesGetPortfolioEntryByIdQuery_WithInvariantDecimalsAndRoundTripUtcDates()
    {
        _sender.Send(Arg.Is<GetPortfolioEntryByIdQuery>(q => q.Id == 9), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioEntryResponse>>(
                new PortfolioEntryResponse(9, 3, 20, 30, 0.50m, 60000.100m, RecordedAt)));

        ErrorOr<IReadOnlyDictionary<string, string?>> result =
            await CreateReader().ReadAsync(PendingActionOperation.UpdateEntry, "9", TestContext.Current.CancellationToken);

        result.Value.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["CryptoCurrencyId"] = "20",
            ["ExchangeId"] = "30",
            ["Quantity"] = "0.5",
            ["PricePerUnit"] = "60000.1",
            ["RecordedAt"] = "2026-11-10T18:30:00.1230000Z",
        });
    }

    [Fact]
    public async Task UpdateNote_DispatchesGetNoteByIdQuery_AndIncludesIsActive()
    {
        _sender.Send(Arg.Is<GetNoteByIdQuery>(q => q.Id == "note-1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<NoteResponse>>(
                new NoteResponse("note-1", "Ideas", "Rebalanceo", "Vender ETH", RecordedAt, false)));

        ErrorOr<IReadOnlyDictionary<string, string?>> result =
            await CreateReader().ReadAsync(PendingActionOperation.UpdateNote, "note-1", TestContext.Current.CancellationToken);

        result.Value.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["Category"] = "Ideas",
            ["Title"] = "Rebalanceo",
            ["Content"] = "Vender ETH",
            ["IsActive"] = "false",
        });
    }

    [Fact]
    public async Task Snapshots_HaveExactlyTheFieldsOfTheFullRequest_SoTheyCompareWithBefore()
    {
        _sender.Send(Arg.Any<GetPortfolioByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioResponse>>(new PortfolioResponse(3, 7, "n", "d", RecordedAt, null)));
        _sender.Send(Arg.Any<GetPortfolioEntryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioEntryResponse>>(new PortfolioEntryResponse(9, 3, 20, 30, 1m, 1m, RecordedAt)));
        _sender.Send(Arg.Any<GetNoteByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<NoteResponse>>(new NoteResponse("n1", "c", "t", "x", RecordedAt, true)));
        CurrentValuesReader reader = CreateReader();
        CancellationToken ct = TestContext.Current.CancellationToken;

        (await reader.ReadAsync(PendingActionOperation.UpdatePortfolio, "3", ct)).Value.Keys.Should().BeEquivalentTo(
            FieldSnapshot.OfRequestFields(new UpdatePortfolioPayload(3, "n", "d")).Keys);
        (await reader.ReadAsync(PendingActionOperation.UpdateEntry, "9", ct)).Value.Keys.Should().BeEquivalentTo(
            FieldSnapshot.OfRequestFields(new UpdateEntryPayload(9, 20, 30, 1m, 1m, RecordedAt)).Keys);
        (await reader.ReadAsync(PendingActionOperation.UpdateNote, "n1", ct)).Value.Keys.Should().BeEquivalentTo(
            FieldSnapshot.OfRequestFields(new UpdateNotePayload("n1", "c", "t", "x", true)).Keys);
    }

    [Fact]
    public async Task AnExternalChangeToAFieldTheUserDidNotEdit_IsDetectedAsStale()
    {
        // Proposed: only Quantity changes. Before snapshot was taken with PricePerUnit = 60000.
        var payload = new UpdateEntryPayload(9, 20, 30, 0.75m, 60000m, RecordedAt);
        IReadOnlyDictionary<string, string?> before = FieldSnapshot.OfRequestFields(payload with { Quantity = 0.5m });
        // Meanwhile someone else changed the price.
        _sender.Send(Arg.Any<GetPortfolioEntryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioEntryResponse>>(new PortfolioEntryResponse(9, 3, 20, 30, 0.5m, 61000m, RecordedAt)));

        ErrorOr<IReadOnlyDictionary<string, string?>> current =
            await CreateReader().ReadAsync(PendingActionOperation.UpdateEntry, "9", TestContext.Current.CancellationToken);

        StaleCheck.IsStale(before, current.Value).Should().BeTrue();
    }

    [Fact]
    public async Task AnUnchangedResource_IsNotStale()
    {
        var payload = new UpdateEntryPayload(9, 20, 30, 0.75m, 60000m, RecordedAt);
        IReadOnlyDictionary<string, string?> before = FieldSnapshot.OfRequestFields(payload with { Quantity = 0.5m });
        _sender.Send(Arg.Any<GetPortfolioEntryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioEntryResponse>>(new PortfolioEntryResponse(9, 3, 20, 30, 0.5m, 60000.00m, RecordedAt)));

        ErrorOr<IReadOnlyDictionary<string, string?>> current =
            await CreateReader().ReadAsync(PendingActionOperation.UpdateEntry, "9", TestContext.Current.CancellationToken);

        StaleCheck.IsStale(before, current.Value).Should().BeFalse();
    }

    [Fact]
    public async Task NotFound_IsPropagated()
    {
        _sender.Send(Arg.Any<GetPortfolioEntryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioEntryResponse>>(Error.NotFound("PortfolioEntry.NotFound", "gone")));

        ErrorOr<IReadOnlyDictionary<string, string?>> result =
            await CreateReader().ReadAsync(PendingActionOperation.UpdateEntry, "9", TestContext.Current.CancellationToken);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Code.Should().Be("PortfolioEntry.NotFound");
    }

    [Theory]
    [InlineData(PendingActionOperation.UpdatePortfolio)]
    [InlineData(PendingActionOperation.UpdateEntry)]
    public async Task ANonNumericTargetIdForANumericResource_IsNotFound_WithoutDispatching(PendingActionOperation op)
    {
        ErrorOr<IReadOnlyDictionary<string, string?>> result =
            await CreateReader().ReadAsync(op, "not-a-number", TestContext.Current.CancellationToken);

        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        await _sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PendingActionOperation.CreatePortfolio)]
    [InlineData(PendingActionOperation.RefreshPrices)]
    public async Task ANonEditOperation_IsAProgrammingError(PendingActionOperation op)
    {
        Func<Task> read = () => CreateReader().ReadAsync(op, "3", TestContext.Current.CancellationToken);

        await read.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
