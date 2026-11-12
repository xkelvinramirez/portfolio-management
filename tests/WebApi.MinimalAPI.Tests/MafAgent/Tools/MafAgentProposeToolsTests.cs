using System.Text.Json;
using Application.Common.Security;
using Application.CryptoCurrencies.Query;
using Application.Exchanges.Query;
using Application.MafAgent;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Application.Portfolios.Query;
using Contracts.Common;
using Contracts.CryptoCurrencies;
using Contracts.Exchanges;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using Infrastructure.MafAgent.Tools;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace WebApi.MinimalAPI.Tests.MafAgent.Tools;

/// <summary>
/// Plan T027 (AC2, AC9, AC17, AC19). A propose tool never writes business data: it validates, resolves the
/// catalog, builds the FULL request (merged with the current values for edits) and stages one pending action.
/// </summary>
public sealed class MafAgentProposeToolsTests
{
    private const long UserId = 7;
    private const long OwnedPortfolioId = 5;
    private const long ForeignPortfolioId = 6;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly IPendingActionRepository _actions = Substitute.For<IPendingActionRepository>();
    private readonly ICurrentValuesReader _reader = Substitute.For<ICurrentValuesReader>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly PendingActionFactory _factory;
    private readonly MafAgentProposeTools _tools;
    private readonly Guid _conversationId = Guid.NewGuid();

    public MafAgentProposeToolsTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(c => "enc:" + c.Arg<string>());
        _encryption.Decrypt(Arg.Any<string>()).Returns(c => c.Arg<string>()["enc:".Length..]);
        _currentUser.UserId.Returns(UserId);
        _factory = new PendingActionFactory(_encryption, _currentUser, _time);

        _sender.Send(Arg.Any<GetPortfolioByIdQuery>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            long id = c.Arg<GetPortfolioByIdQuery>().Id;
            ErrorOr<PortfolioResponse> result = id == OwnedPortfolioId
                ? new PortfolioResponse(id, UserId, "Principal", "Largo plazo", DateTime.UtcNow, null)
                : Error.NotFound("Portfolio.NotFound");
            return Task.FromResult(result);
        });
        Catalog(
            [new(1, "BTC", "Bitcoin", null, null, DateTime.UtcNow), new(2, "ETH", "Ethereum", null, null, DateTime.UtcNow)],
            [new(10, "Binance", "key", DateTime.UtcNow), new(11, " Kraken ", "key", DateTime.UtcNow)]);

        _tools = new MafAgentProposeTools(_factory, _actions, _reader, _sender, _time);
        _tools.BeginTurn(_conversationId);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void Catalog(CryptoCurrencyResponse[] cryptos, ExchangeResponse[] exchanges)
    {
        _sender.Send(Arg.Any<GetCryptoCurrenciesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<CryptoCurrencyResponse>>>(new PaginatorResponse<CryptoCurrencyResponse>
            {
                Page = 1,
                PageSize = 100,
                Total = cryptos.Length,
                TotalPages = 1,
                Data = cryptos,
            }));
        _sender.Send(Arg.Any<GetExchangesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<ExchangeResponse>>>(new PaginatorResponse<ExchangeResponse>
            {
                Page = 1,
                PageSize = 100,
                Total = exchanges.Length,
                TotalPages = 1,
                Data = exchanges,
            }));
    }

    private PendingAction Staged() => _tools.ProposedActions.Should().ContainSingle().Subject;

    private T PayloadOf<T>(PendingAction action) => (T)_factory.ReadPayload(action);

    private void NothingStaged()
    {
        _tools.ProposedActions.Should().BeEmpty();
        _actions.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // No command ever leaves a propose tool: only queries (AC2).
    private void OnlyQueriesWereSent() =>
        _sender.ReceivedCalls().Select(c => c.GetArguments()[0]!.GetType().Name)
            .Should().OnlyContain(name => name.EndsWith("Query"));

    private void CurrentValues(PendingActionOperation op, string targetId, Dictionary<string, string?> values) =>
        _reader.ReadAsync(op, targetId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(values));

    private void EntryNow() => CurrentValues(PendingActionOperation.UpdateEntry, "9", new()
    {
        ["CryptoCurrencyId"] = "1",
        ["ExchangeId"] = "10",
        ["Quantity"] = "0.5",
        ["PricePerUnit"] = "60000",
        ["RecordedAt"] = "2026-11-01T08:00:00.0000000Z",
    });

    // ---- creates ----

    [Fact]
    public async Task ProposeCreateEntry_WithCompleteData_StagesOneAction_AndWritesNothing()
    {
        object result = await _tools.ProposeCreateEntry(OwnedPortfolioId, "btc", "binance", 0.5m, 65000m, "2026-11-10T18:30:00Z", Token);

        PendingAction action = Staged();
        _actions.Received(1).Add(action);
        action.ConversationId.Should().Be(_conversationId);
        action.Operation.Should().Be(PendingActionOperation.CreateEntry);
        PayloadOf<CreateEntryPayload>(action).Should().Be(new CreateEntryPayload(
            OwnedPortfolioId, 1, 10, 0.5m, 65000m, new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc)));
        result.Should().BeEquivalentTo(new { Id = action.Id, Operation = "CreateEntry" });
        OnlyQueriesWereSent();
    }

    [Theory]
    [InlineData(null, "binance", 0.5, 65000.0, "2026-11-10T18:30:00Z", "MissingOrAmbiguous:symbol")]
    [InlineData("BTC", " ", 0.5, 65000.0, "2026-11-10T18:30:00Z", "MissingOrAmbiguous:exchangeName")]
    [InlineData("BTC", "binance", null, 65000.0, "2026-11-10T18:30:00Z", "MissingOrAmbiguous:quantity")]
    [InlineData("BTC", "binance", 0.0, 65000.0, "2026-11-10T18:30:00Z", "MissingOrAmbiguous:quantity")]
    [InlineData("BTC", "binance", 0.5, null, "2026-11-10T18:30:00Z", "MissingOrAmbiguous:pricePerUnit")]
    [InlineData("BTC", "binance", 0.5, -1.0, "2026-11-10T18:30:00Z", "MissingOrAmbiguous:pricePerUnit")]
    [InlineData("BTC", "binance", 0.5, 65000.0, null, "MissingOrAmbiguous:recordedAt")]
    [InlineData("BTC", "binance", 0.5, 65000.0, "ayer", "MissingOrAmbiguous:recordedAt")]
    [InlineData("DOGE", "binance", 0.5, 65000.0, "2026-11-10T18:30:00Z", "UnknownCatalogItem:DOGE")]
    [InlineData("BTC", "Coinbase", 0.5, 65000.0, "2026-11-10T18:30:00Z", "UnknownCatalogItem:Coinbase")]
    public async Task ProposeCreateEntry_MissingOrUnknownData_StagesNothing_AndNamesTheField(
        string? symbol, string? exchange, double? quantity, double? price, string? recordedAt, string expected)
    {
        object result = await _tools.ProposeCreateEntry(
            OwnedPortfolioId, symbol, exchange, (decimal?)quantity, (decimal?)price, recordedAt, Token);

        result.Should().Be(expected);
        NothingStaged();
    }

    [Fact]
    public async Task ProposeCreateEntry_MissingPortfolio_IsMissingOrAmbiguous()
    {
        (await _tools.ProposeCreateEntry(null, "BTC", "binance", 1m, 1m, "2026-11-10T18:30:00Z", Token))
            .Should().Be("MissingOrAmbiguous:portfolioId");
        NothingStaged();
    }

    [Fact]
    public async Task ProposeCreateEntry_InAnotherUsersPortfolio_IsNotFound()
    {
        (await _tools.ProposeCreateEntry(ForeignPortfolioId, "BTC", "binance", 1m, 1m, "2026-11-10T18:30:00Z", Token))
            .Should().Be("NotFound");
        NothingStaged();
    }

    [Fact]
    public async Task AnAmbiguousSymbol_IsMissingOrAmbiguous()
    {
        Catalog(
            [new(1, "BTC", "Bitcoin", null, null, DateTime.UtcNow), new(3, "btc", "Bitcoin (wrapped)", null, null, DateTime.UtcNow)],
            [new(10, "Binance", "key", DateTime.UtcNow)]);

        (await _tools.ProposeCreateEntry(OwnedPortfolioId, "BTC", "Binance", 1m, 1m, "2026-11-10T18:30:00Z", Token))
            .Should().Be("MissingOrAmbiguous:symbol");
        NothingStaged();
    }

    [Fact]
    public async Task ExchangeName_IsMatchedTrimmed_AndCaseInsensitive()
    {
        await _tools.ProposeCreateEntry(OwnedPortfolioId, "ETH", "  KRAKEN", 1m, 1m, "2026-11-10T18:30:00Z", Token);

        PayloadOf<CreateEntryPayload>(Staged()).Should().Match<CreateEntryPayload>(p => p.CryptoCurrencyId == 2 && p.ExchangeId == 11);
    }

    [Theory]
    [InlineData("2026-11-10T18:30:00Z")]
    [InlineData("2026-11-10T19:30:00+01:00")]
    [InlineData("2026-11-10T19:30:00")] // no offset: Europe/Madrid (UTC+1 in November)
    public async Task Dates_WithZ_WithOffset_OrWithoutOffsetInMadrid_AllBecomeTheSameUtcInstant(string recordedAt)
    {
        await _tools.ProposeCreateEntry(OwnedPortfolioId, "BTC", "Binance", 1m, 1m, recordedAt, Token);

        DateTime stored = PayloadOf<CreateEntryPayload>(Staged()).RecordedAt;
        stored.Should().Be(new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc));
        stored.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ADateWithoutOffset_InMadridSummerTime_UsesTheSummerOffset()
    {
        await _tools.ProposeCreateEntry(OwnedPortfolioId, "BTC", "Binance", 1m, 1m, "2026-07-01T12:00:00", Token);

        PayloadOf<CreateEntryPayload>(Staged()).RecordedAt.Should().Be(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ThreeProposalsInOneTurn_AreThreeIndependentActions()
    {
        await _tools.ProposeCreateEntry(OwnedPortfolioId, "BTC", "Binance", 1m, 1m, "2026-11-10T18:30:00Z", Token);
        await _tools.ProposeCreateEntry(OwnedPortfolioId, "ETH", "Binance", 2m, 2m, "2026-11-10T18:30:00Z", Token);
        await _tools.ProposeCreatePortfolio("Ahorro", null, Token);

        _tools.ProposedActions.Should().HaveCount(3);
        _tools.ProposedActions.Select(a => a.Id).Should().OnlyHaveUniqueItems();
        _actions.Received(3).Add(Arg.Any<PendingAction>());
    }

    [Fact]
    public async Task BeginTurn_StartsAnEmptyListForTheNewTurn()
    {
        await _tools.ProposeCreatePortfolio("Ahorro", "x", Token);
        _tools.ProposedActions.Should().HaveCount(1);

        _tools.BeginTurn(Guid.NewGuid());

        _tools.ProposedActions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProposingBeforeBeginTurn_IsAProgrammingError()
    {
        var tools = new MafAgentProposeTools(_factory, _actions, _reader, _sender, _time);

        Func<Task> act = () => tools.ProposeCreatePortfolio("Ahorro", "x", Token);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ProposeCreatePortfolio_WithoutDescription_UsesAnEmptyOne_AndWithoutNameAsksForIt()
    {
        (await _tools.ProposeCreatePortfolio(" ", "x", Token)).Should().Be("MissingOrAmbiguous:name");
        NothingStaged();

        await _tools.ProposeCreatePortfolio("Ahorro", null, Token);
        PayloadOf<CreatePortfolioPayload>(Staged()).Should().Be(new CreatePortfolioPayload("Ahorro", ""));
    }

    [Theory]
    [InlineData(null, "t", "c", "MissingOrAmbiguous:category")]
    [InlineData("k", "", "c", "MissingOrAmbiguous:title")]
    [InlineData("k", "t", null, "MissingOrAmbiguous:content")]
    public async Task ProposeCreateNote_NeedsCategoryTitleAndContent(string? category, string? title, string? content, string expected)
    {
        (await _tools.ProposeCreateNote(category, title, content, Token)).Should().Be(expected);
        NothingStaged();
    }

    [Fact]
    public async Task ProposeCreateNote_TheSummaryNeverRepeatsTheTitleOrTheContent()
    {
        object result = await _tools.ProposeCreateNote("Ideas", "Mi título secreto", "Mi contenido secreto", Token);

        PayloadOf<CreateNotePayload>(Staged()).Should().Be(new CreateNotePayload("Ideas", "Mi título secreto", "Mi contenido secreto"));
        _factory.ToDto(Staged()).Summary.Should().NotContain("secreto");
        result.Should().NotBeOfType<string>();
    }

    // ---- edits: the FULL request, merged with the current values ----

    [Fact]
    public async Task ProposeUpdateEntry_ChangingOnlyQuantity_KeepsEveryOtherField_AndBeforeIsTheCurrentState()
    {
        EntryNow();

        object result = await _tools.ProposeUpdateEntry(9, quantity: 0.75m, cancellationToken: Token);

        PendingAction action = Staged();
        PayloadOf<UpdateEntryPayload>(action).Should().Be(new UpdateEntryPayload(
            9, 1, 10, 0.75m, 60000m, new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)));
        _factory.ReadBefore(action).Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["CryptoCurrencyId"] = "1",
            ["ExchangeId"] = "10",
            ["Quantity"] = "0.5",
            ["PricePerUnit"] = "60000",
            ["RecordedAt"] = "2026-11-01T08:00:00.0000000Z",
        });
        result.Should().BeEquivalentTo(new { Operation = "UpdateEntry" });
        OnlyQueriesWereSent();
    }

    [Fact]
    public async Task ProposeUpdateEntry_ResolvesANewSymbolAndExchange()
    {
        EntryNow();

        await _tools.ProposeUpdateEntry(9, symbol: "eth", exchangeName: "kraken", cancellationToken: Token);

        PayloadOf<UpdateEntryPayload>(Staged()).Should().Match<UpdateEntryPayload>(p => p.CryptoCurrencyId == 2 && p.ExchangeId == 11 && p.Quantity == 0.5m);
    }

    [Fact]
    public async Task ProposeUpdateEntry_WithAnUnknownExchange_IsUnknownCatalogItem()
    {
        EntryNow();

        (await _tools.ProposeUpdateEntry(9, exchangeName: "Coinbase", cancellationToken: Token)).Should().Be("UnknownCatalogItem:Coinbase");
        NothingStaged();
    }

    [Fact]
    public async Task ProposeUpdateEntry_WithAnUnknownSymbol_IsUnknownCatalogItem()
    {
        EntryNow();

        (await _tools.ProposeUpdateEntry(9, symbol: "DOGE", cancellationToken: Token)).Should().Be("UnknownCatalogItem:DOGE");
        NothingStaged();
    }

    [Fact]
    public async Task ProposeUpdatePortfolio_ChangingOnlyTheName_KeepsTheDescription()
    {
        CurrentValues(PendingActionOperation.UpdatePortfolio, "5", new() { ["Name"] = "Principal", ["Description"] = "Largo plazo" });

        await _tools.ProposeUpdatePortfolio(OwnedPortfolioId, name: "Jubilación", cancellationToken: Token);

        PayloadOf<UpdatePortfolioPayload>(Staged()).Should().Be(new UpdatePortfolioPayload(OwnedPortfolioId, "Jubilación", "Largo plazo"));
    }
    [Fact]
    public async Task ProposeUpdateEntry_WithoutRealChanges_IsMissingOrAmbiguousChanges()
    {
        EntryNow();

        (await _tools.ProposeUpdateEntry(9, cancellationToken: Token)).Should().Be("MissingOrAmbiguous:changes");
        (await _tools.ProposeUpdateEntry(9, quantity: 0.50m, symbol: "BTC", cancellationToken: Token)).Should().Be("MissingOrAmbiguous:changes");
        NothingStaged();
    }

    [Fact]
    public async Task ProposeUpdateEntry_OfAnUnknownOrForeignEntry_IsNotFound()
    {
        _reader.ReadAsync(PendingActionOperation.UpdateEntry, "8", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<IReadOnlyDictionary<string, string?>>>(Error.NotFound("PortfolioEntry.NotFound")));

        (await _tools.ProposeUpdateEntry(8, quantity: 1m, cancellationToken: Token)).Should().Be("NotFound");
        NothingStaged();
    }

    [Fact]
    public async Task ProposeUpdatePortfolio_MergesOnlyTheGivenField()
    {
        CurrentValues(PendingActionOperation.UpdatePortfolio, "5", new() { ["Name"] = "Principal", ["Description"] = "Largo plazo" });

        await _tools.ProposeUpdatePortfolio(OwnedPortfolioId, description: "Jubilación", cancellationToken: Token);

        PayloadOf<UpdatePortfolioPayload>(Staged()).Should().Be(new UpdatePortfolioPayload(OwnedPortfolioId, "Principal", "Jubilación"));
    }

    [Fact]
    public async Task ProposeUpdateNote_OfAnInactiveNote_KeepsItInactive()
    {
        CurrentValues(PendingActionOperation.UpdateNote, "n1", new()
        {
            ["Category"] = "Ideas",
            ["Title"] = "Viejo",
            ["Content"] = "Texto",
            ["IsActive"] = "false",
        });

        await _tools.ProposeUpdateNote("n1", title: "Nuevo", cancellationToken: Token);

        PayloadOf<UpdateNotePayload>(Staged()).Should().Be(new UpdateNotePayload("n1", "Ideas", "Nuevo", "Texto", false));
        _factory.ToDto(Staged()).Summary.Should().NotContain("Nuevo").And.NotContain("Viejo");
    }

    // ---- system actions ----

    [Fact]
    public async Task ProposeRefreshPrices_OfAnOwnedPortfolio_Stages_AndOfAForeignOneIsNotFound()
    {
        (await _tools.ProposeRefreshPrices(ForeignPortfolioId, Token)).Should().Be("NotFound");
        NothingStaged();

        await _tools.ProposeRefreshPrices(OwnedPortfolioId, Token);
        PayloadOf<RefreshPricesPayload>(Staged()).Should().Be(new RefreshPricesPayload(OwnedPortfolioId));
    }

    [Fact]
    public async Task ProposeSendStatusReport_WithoutPeriod_StoresThePreviousCalendarMonthResolvedNow()
    {
        await _tools.ProposeSendStatusReport(OwnedPortfolioId, null, null, Token);

        PayloadOf<SendStatusReportPayload>(Staged()).Should().Be(new SendStatusReportPayload(
            OwnedPortfolioId, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task ProposeSendStatusReport_WithAPeriod_StoresItInUtc()
    {
        await _tools.ProposeSendStatusReport(OwnedPortfolioId, "2026-09-01T00:00:00Z", "2026-09-30T00:00:00Z", Token);

        PayloadOf<SendStatusReportPayload>(Staged()).Should().Be(new SendStatusReportPayload(
            OwnedPortfolioId, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("2026-09-01T00:00:00Z", null)]
    [InlineData("2026-09-30T00:00:00Z", "2026-09-01T00:00:00Z")]
    public async Task ProposeSendStatusReport_HalfOrReversedPeriod_IsMissingOrAmbiguousPeriod(string? start, string? end)
    {
        (await _tools.ProposeSendStatusReport(OwnedPortfolioId, start, end, Token)).Should().Be("MissingOrAmbiguous:period");
        NothingStaged();
    }

    [Fact]
    public async Task ProposeSendStatusReport_DateOnlyBounds_AreThoseCalendarDays_NotMadridMidnight()
    {
        await _tools.ProposeSendStatusReport(OwnedPortfolioId, "2026-09-01", "2026-09-30", Token);

        PayloadOf<SendStatusReportPayload>(Staged()).Should().Be(new SendStatusReportPayload(
            OwnedPortfolioId, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(0.0, null, null, "MissingOrAmbiguous:quantity")]
    [InlineData(null, -0.01, null, "MissingOrAmbiguous:pricePerUnit")]
    [InlineData(null, null, "mañana", "MissingOrAmbiguous:recordedAt")]
    [InlineData(null, null, "2026-03-29T02:30:00", "MissingOrAmbiguous:recordedAt")] // skipped by the Madrid spring-forward change
    public async Task ProposeUpdateEntry_InvalidChanges_StageNothing(double? quantity, double? price, string? recordedAt, string expected)
    {
        EntryNow();

        (await _tools.ProposeUpdateEntry(9, (decimal?)quantity, (decimal?)price, recordedAt, cancellationToken: Token)).Should().Be(expected);
        NothingStaged();
    }

    [Fact]
    public async Task ProposeCreateEntry_WithAZeroPrice_IsAccepted()
    {
        await _tools.ProposeCreateEntry(OwnedPortfolioId, "BTC", "Binance", 1m, 0m, "2026-11-10T18:30:00Z", Token);

        PayloadOf<CreateEntryPayload>(Staged()).PricePerUnit.Should().Be(0m);
    }

    [Fact]
    public async Task MissingTargets_AreMissingOrAmbiguous()
    {
        (await _tools.ProposeUpdateEntry(null, quantity: 1m, cancellationToken: Token)).Should().Be("MissingOrAmbiguous:entryId");
        (await _tools.ProposeUpdateNote(" ", title: "x", cancellationToken: Token)).Should().Be("MissingOrAmbiguous:noteId");
        (await _tools.ProposeUpdatePortfolio(null, name: "x", cancellationToken: Token)).Should().Be("MissingOrAmbiguous:portfolioId");
        (await _tools.ProposeRefreshPrices(null, Token)).Should().Be("MissingOrAmbiguous:portfolioId");
        (await _tools.ProposeSendStatusReport(null, null, null, Token)).Should().Be("MissingOrAmbiguous:portfolioId");
        NothingStaged();
    }
    // ---- the AI functions ----

    [Fact]
    public void GetTools_ExposesExactlyTheProposeToolsOfThePlan_WithoutAnyUserId()
    {
        IReadOnlyList<AIFunction> functions = _tools.GetTools();

        functions.Select(f => f.Name).Should().BeEquivalentTo(
        [
            "ProposeCreatePortfolio", "ProposeUpdatePortfolio", "ProposeCreateEntry", "ProposeUpdateEntry",
            "ProposeCreateNote", "ProposeUpdateNote", "ProposeRefreshPrices", "ProposeSendStatusReport",
        ]);
        functions.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Description));
        functions.Select(f => f.JsonSchema.GetRawText()).Should().NotContain(s => s.Contains("userId", StringComparison.OrdinalIgnoreCase));
        typeof(MafAgentProposeTools).GetMethods()
            .SelectMany(m => m.GetParameters())
            .Should().NotContain(p => p.Name!.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InvokedThroughTheAIFunction_ADateWithoutOffsetIsStillReadInMadrid()
    {
        AIFunction create = _tools.GetTools().Single(f => f.Name == "ProposeCreateEntry");

        await create.InvokeAsync(new AIFunctionArguments
        {
            ["portfolioId"] = OwnedPortfolioId,
            ["symbol"] = "BTC",
            ["exchangeName"] = "Binance",
            ["quantity"] = 1m,
            ["pricePerUnit"] = 1m,
            ["recordedAt"] = "2026-11-10T19:30:00",
        }, Token);

        PayloadOf<CreateEntryPayload>(Staged()).RecordedAt.Should().Be(new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc));
        JsonSerializer.Serialize(PayloadOf<CreateEntryPayload>(Staged())).Should().Contain("2026-11-10T18:30:00");
    }
}
