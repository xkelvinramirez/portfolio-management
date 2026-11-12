using System.Linq.Expressions;
using System.Text.Json;
using Application.Common.Security;
using Application.CryptoCurrencies.Query;
using Application.Exchanges.Query;
using Application.Notes.Query;
using Application.PortfolioEntries.Interfaces;
using Application.PortfolioEntries.Query;
using Application.Portfolios.Interfaces;
using Application.Portfolios.Query;
using Contracts.Common;
using Contracts.CryptoCurrencies;
using Contracts.Exchanges;
using Contracts.Notes;
using Contracts.PortfolioEntries;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using Infrastructure.Agent.Plugins;
using Infrastructure.MafAgent.Tools;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace WebApi.MinimalAPI.Tests.MafAgent.Tools;

/// <summary>
/// Plan T025 (AC1, AC1b). The five inherited read tools must return the same data as their Semantic Kernel
/// counterparts in <see cref="PortfolioAgentPlugin"/> for the same seed: both are built here over the SAME
/// repository doubles and compared. The other read tools go through the existing queries.
/// </summary>
public sealed class MafAgentReadToolsTests
{
    private const long UserId = 42;
    // Portfolio.Create leaves Id = 0, and the Domain valuation methods filter entries by that Id (as in PortfolioAgentPluginTests).
    private const long PortfolioId = 0;
    private const long ForeignPortfolioId = 6;

    private readonly IPortfolioRepository _portfolios = Substitute.For<IPortfolioRepository>();
    private readonly IPortfolioEntryRepository _entries = Substitute.For<IPortfolioEntryRepository>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly MafAgentReadTools _tools;
    private readonly PortfolioAgentPlugin _sk;

    public MafAgentReadToolsTests()
    {
        _currentUser.UserId.Returns(UserId);

        Portfolio main = Portfolio.Create(UserId, "Principal", "Largo plazo");
        Portfolio second = Portfolio.Create(UserId, "Trading", "Corto plazo");
        _portfolios.GetByIdAsync(PortfolioId, Arg.Any<CancellationToken>()).Returns(main);
        _portfolios.GetByIdAsync(ForeignPortfolioId, Arg.Any<CancellationToken>()).Returns(Portfolio.Create(99, "Ajeno", string.Empty));
        _portfolios.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Expression<Func<Portfolio, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatorResponse<Portfolio> { Page = 1, PageSize = 50, Total = 2, TotalPages = 1, Data = [main, second] });

        // The multi-exchange seed of PortfolioAgentPluginTests: BTC on two exchanges, ETH constant, SOL only at the end.
        _entries.GetByPortfolioIdAsync(PortfolioId, Arg.Any<CancellationToken>()).Returns(_ =>
        [
            Entry(1, 1, "BTC", 2m, 100m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry(1, 2, "BTC", 1m, 50m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry(1, 1, "BTC", 1m, 120m, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry(2, 1, "ETH", 1m, 200m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
            Entry(2, 1, "ETH", 1m, 260m, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)),
            Entry(3, 1, "SOL", 5m, 10m, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)),
        ]);

        _tools = new MafAgentReadTools(_portfolios, _entries, _sender, _currentUser, _time);
        _sk = new PortfolioAgentPlugin(_portfolios, _entries, _currentUser);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PortfolioEntry Entry(long cryptoId, long exchangeId, string symbol, decimal quantity, decimal price, DateTime at)
    {
        PortfolioEntry entry = PortfolioEntry.Create(PortfolioId, cryptoId, exchangeId, quantity, price, at);
        entry.CryptoCurrency = CryptoCurrency.Create(symbol, symbol);
        return entry;
    }

    // ---- parity with the SK plugin (AC1) ----

    [Fact]
    public async Task ListMyPortfolios_MatchesTheSkPlugin()
    {
        object result = await _tools.ListMyPortfolios(Token);

        result.Should().BeEquivalentTo(await _sk.ListMyPortfoliosAsync(Token), o => o.WithStrictOrdering());
        ((IEnumerable<object>)result).Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPortfolioValue_MatchesTheSkPlugin()
    {
        object result = await _tools.GetPortfolioValue(PortfolioId, Token);

        result.Should().Be(await _sk.GetPortfolioValueAsync(PortfolioId, Token));
    }

    [Fact]
    public async Task GetPortfolioHoldings_MatchesTheSkPlugin_OrderedByValueDescending()
    {
        object result = await _tools.GetPortfolioHoldings(PortfolioId, Token);

        result.Should().BeEquivalentTo(await _sk.GetPortfolioHoldingsAsync(PortfolioId, Token), o => o.WithStrictOrdering());
        ((IEnumerable<object>)result).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("BTC", "max", null, null)]
    [InlineData("btc", "min", null, null)]
    [InlineData("ETH", "max", "2026-10-01", "2026-10-10")]
    [InlineData("ETH", "highest", null, null)]
    [InlineData("SOL", "min", "2026-10-20", null)]
    [InlineData("ETH", " MIN ", null, null)]
    public async Task GetAssetPeakOrTroughDate_MatchesTheSkPlugin(string symbol, string extreme, string? from, string? to)
    {
        object result = await _tools.GetAssetPeakOrTroughDate(PortfolioId, symbol, extreme, from, to, Token);

        AssetValuePointForAgent? sk = await _sk.GetAssetPeakOrTroughDateAsync(PortfolioId, symbol, extreme, from, to, Token);
        sk.Should().NotBeNull("the seed must make this case meaningful");
        result.Should().BeEquivalentTo(sk);
    }

    [Fact]
    public async Task GetAssetPeakOrTroughDate_UnknownSymbol_IsNotFound_LikeTheSkNull()
    {
        object result = await _tools.GetAssetPeakOrTroughDate(PortfolioId, "DOGE", "max", null, null, Token);

        (await _sk.GetAssetPeakOrTroughDateAsync(PortfolioId, "DOGE", "max", null, null, Token)).Should().BeNull();
        result.Should().Be("NotFound");
    }

    [Fact]
    public async Task GetPortfolioAssetValueComparison_MatchesTheSkPlugin_AcrossExchanges_AndAnAssetOnOneDateOnly()
    {
        object result = await _tools.GetPortfolioAssetValueComparison(PortfolioId, "2026-10-06", "2026-11-05", Token);

        result.Should().BeEquivalentTo(
            await _sk.GetPortfolioAssetValueComparisonAsync(PortfolioId, "2026-10-06", "2026-11-05", Token),
            o => o.WithStrictOrdering());
        result.Should().BeEquivalentTo(new[]
        {
            new { Symbol = "BTC", StartValue = 250m, EndValue = 170m, Difference = -80m },
            new { Symbol = "ETH", StartValue = 200m, EndValue = 260m, Difference = 60m },
            new { Symbol = "SOL", StartValue = 0m, EndValue = 50m, Difference = 50m },
        }, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData("06/10/2026", "2026-11-05")]
    [InlineData("2026-10-06", "2026-13-05")]
    [InlineData("2026-11-05", "2026-10-06")]
    public async Task GetPortfolioAssetValueComparison_RejectsInvalidOrReversedDates_LikeTheSkPlugin(string from, string to)
    {
        object result = await _tools.GetPortfolioAssetValueComparison(PortfolioId, from, to, Token);

        Func<Task> sk = () => _sk.GetPortfolioAssetValueComparisonAsync(PortfolioId, from, to, Token);
        await sk.Should().ThrowAsync<ArgumentException>();
        result.Should().Be("InvalidDates");
    }

    // ---- ownership (AC1b): another user's portfolio is NotFound and its entries are never read ----

    public static TheoryData<string> PortfolioTools =>
        ["value", "holdings", "peak", "comparison"];

    [Theory]
    [MemberData(nameof(PortfolioTools))]
    public async Task AnotherUsersPortfolio_IsNotFound(string tool)
    {
        object result = tool switch
        {
            "value" => await _tools.GetPortfolioValue(ForeignPortfolioId, Token),
            "holdings" => await _tools.GetPortfolioHoldings(ForeignPortfolioId, Token),
            "peak" => await _tools.GetAssetPeakOrTroughDate(ForeignPortfolioId, "BTC", "max", null, null, Token),
            _ => await _tools.GetPortfolioAssetValueComparison(ForeignPortfolioId, "2026-10-06", "2026-11-05", Token),
        };

        result.Should().Be("NotFound");
        await _entries.DidNotReceiveWithAnyArgs().GetByPortfolioIdAsync(default, Token);
    }

    [Fact]
    public async Task UnknownPortfolio_IsNotFound()
    {
        (await _tools.GetPortfolioValue(777, Token)).Should().Be("NotFound");
    }

    // ---- the other read tools: existing queries ----

    [Fact]
    public async Task GetPortfolioEntries_ReadsEveryPage_OfTheExistingQuery()
    {
        PortfolioEntryResponse first = new(1, PortfolioId, 1, 1, 2m, 100m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        PortfolioEntryResponse second = new(2, PortfolioId, 2, 1, 1m, 200m, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
        _sender.Send(Arg.Any<GetPortfolioEntriesQuery>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            GetPortfolioEntriesQuery q = c.Arg<GetPortfolioEntriesQuery>();
            ErrorOr<PaginatorResponse<PortfolioEntryResponse>> page = new PaginatorResponse<PortfolioEntryResponse>
            {
                Page = q.Paginator.Page,
                PageSize = 1,
                Total = 2,
                TotalPages = 2,
                Data = q.Paginator.Page == 1 ? [first] : [second],
            };
            return Task.FromResult(page);
        });

        object result = await _tools.GetPortfolioEntries(PortfolioId, Token);

        result.Should().BeEquivalentTo(new[]
        {
            new { Id = 1L, CryptoCurrencyId = 1L, ExchangeId = 1L, Quantity = 2m, PricePerUnit = 100m, RecordedAt = first.RecordedAt },
            new { Id = 2L, CryptoCurrencyId = 2L, ExchangeId = 1L, Quantity = 1m, PricePerUnit = 200m, RecordedAt = second.RecordedAt },
        }, o => o.WithStrictOrdering());
        await _sender.Received(1).Send(Arg.Is<GetPortfolioEntriesQuery>(q => q.PortfolioId == PortfolioId && q.Paginator.Page == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPortfolioEntries_OfAnotherUser_IsNotFound()
    {
        _sender.Send(Arg.Any<GetPortfolioEntriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<PortfolioEntryResponse>>>(Error.NotFound("Portfolio.NotFound")));

        (await _tools.GetPortfolioEntries(ForeignPortfolioId, Token)).Should().Be("NotFound");
    }

    [Fact]
    public async Task GetPortfolioHistory_FiltersThePointsByTheOptionalRange()
    {
        HistoryReturns(new PortfolioHistoryResponse(PortfolioId,
        [
            new(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 1m),
            new(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc), 2m),
            new(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 3m),
        ], []));

        object all = await _tools.GetPortfolioHistory(PortfolioId, null, null, Token);
        object ranged = await _tools.GetPortfolioHistory(PortfolioId, "2026-10-15", "2026-11-01", Token);

        ((IEnumerable<object>)all).Should().HaveCount(3);
        ranged.Should().BeEquivalentTo(new[] { new { Value = 2m }, new { Value = 3m } }, o => o.WithStrictOrdering());
        (await _tools.GetPortfolioHistory(PortfolioId, "2026-10-02", "2026-10-31", Token))
            .Should().BeEquivalentTo(new[] { new { Value = 2m } });
        await _sender.Received().Send(Arg.Is<GetPortfolioHistoryQuery>(q => q.Id == PortfolioId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("15/10/2026", null)]
    [InlineData("2026-11-01", "2026-10-15")]
    public async Task GetPortfolioHistory_InvalidOrReversedRange_IsInvalidDates(string? from, string? to)
    {
        HistoryReturns(new PortfolioHistoryResponse(PortfolioId, [], []));

        (await _tools.GetPortfolioHistory(PortfolioId, from, to, Token)).Should().Be("InvalidDates");
    }

    [Fact]
    public async Task GetPortfolioHistory_OfAnotherUser_IsNotFound()
    {
        _sender.Send(Arg.Any<GetPortfolioHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioHistoryResponse>>(Error.NotFound("Portfolio.NotFound")));

        (await _tools.GetPortfolioHistory(ForeignPortfolioId, null, null, Token)).Should().Be("NotFound");
    }

    [Fact]
    public async Task GetPortfolioAllocation_ByAssetToday()
    {
        _sender.Send(Arg.Any<GetPortfolioAllocationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioAllocationResponse>>(new PortfolioAllocationResponse(
                PortfolioId, _time.GetUtcNow().UtcDateTime.Date, PortfolioAllocationGroupBy.Asset,
                [new(1, "BTC", 170m, 0.6m), new(2, "ETH", 113m, 0.4m)])));

        object result = await _tools.GetPortfolioAllocation(PortfolioId, Token);

        result.Should().BeEquivalentTo(new[]
        {
            new { Label = "BTC", Value = 170m, Percentage = 0.6m },
            new { Label = "ETH", Value = 113m, Percentage = 0.4m },
        }, o => o.WithStrictOrdering());
        await _sender.Received(1).Send(
            Arg.Is<GetPortfolioAllocationQuery>(q => q.Id == PortfolioId && q.GroupBy == PortfolioAllocationGroupBy.Asset),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListCryptoCurrencies_ReturnsIdSymbolAndName()
    {
        _sender.Send(Arg.Any<GetCryptoCurrenciesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<CryptoCurrencyResponse>>>(new PaginatorResponse<CryptoCurrencyResponse>
            {
                Page = 1,
                PageSize = 100,
                Total = 1,
                TotalPages = 1,
                Data = [new CryptoCurrencyResponse(1, "BTC", "Bitcoin", "bitcoin", null, DateTime.UtcNow)],
            }));

        object result = await _tools.ListCryptoCurrencies(Token);

        result.Should().BeEquivalentTo(new[] { new { Id = 1L, Symbol = "BTC", Name = "Bitcoin" } });
    }

    [Fact]
    public async Task ListExchanges_NeverExposesTheApiKey()
    {
        _sender.Send(Arg.Any<GetExchangesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<ExchangeResponse>>>(new PaginatorResponse<ExchangeResponse>
            {
                Page = 1,
                PageSize = 100,
                Total = 1,
                TotalPages = 1,
                Data = [new ExchangeResponse(1, "Binance", "super-secret-api-key", DateTime.UtcNow)],
            }));

        object result = await _tools.ListExchanges(Token);

        result.Should().BeEquivalentTo(new[] { new { Id = 1L, Name = "Binance" } });
        JsonSerializer.Serialize(result).Should().NotContain("super-secret-api-key").And.NotContain("ApiKey");
    }

    [Fact]
    public async Task ListNotes_ReturnsEveryNote_WithoutItsContent()
    {
        _sender.Send(Arg.Any<GetNotesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<NoteResponse>>>(new PaginatorResponse<NoteResponse>
            {
                Page = 1,
                PageSize = 100,
                Total = 2,
                TotalPages = 1,
                Data =
                [
                    new NoteResponse("n1", "Ideas", "Comprar", "contenido privado", DateTime.UtcNow, true),
                    new NoteResponse("n2", "Ideas", "Vender", "otro contenido", DateTime.UtcNow, false),
                ],
            }));

        object result = await _tools.ListNotes(Token);

        result.Should().BeEquivalentTo(new[]
        {
            new { Id = "n1", Category = "Ideas", Title = "Comprar", IsActive = true },
            new { Id = "n2", Category = "Ideas", Title = "Vender", IsActive = false },
        });
        JsonSerializer.Serialize(result).Should().NotContain("contenido");
        await _sender.Received().Send(Arg.Is<GetNotesQuery>(q => q.Category == null && q.IsActive == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNote_IncludesTheContent_AndAnUnknownNoteIsNotFound()
    {
        _sender.Send(Arg.Is<GetNoteByIdQuery>(q => q.Id == "n1"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<NoteResponse>>(new NoteResponse("n1", "Ideas", "Comprar", "contenido", DateTime.UtcNow, true)));
        _sender.Send(Arg.Is<GetNoteByIdQuery>(q => q.Id == "zz"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<NoteResponse>>(Error.NotFound("Note.NotFound")));

        (await _tools.GetNote("n1", Token)).Should().BeEquivalentTo(new { Id = "n1", Title = "Comprar", Content = "contenido", IsActive = true });
        (await _tools.GetNote("zz", Token)).Should().Be("NotFound");
    }

    // ---- the AI functions the agent sees ----

    [Fact]
    public void GetTools_ExposesExactlyTheReadToolsOfThePlan_WithoutAsyncSuffixOrUserId()
    {
        IReadOnlyList<AIFunction> functions = _tools.GetTools();

        functions.Select(f => f.Name).Should().BeEquivalentTo(
        [
            "ListMyPortfolios", "GetPortfolioValue", "GetPortfolioHoldings", "GetAssetPeakOrTroughDate",
            "GetPortfolioAssetValueComparison", "GetPortfolioEntries", "GetPortfolioHistory", "GetPortfolioAllocation",
            "ListCryptoCurrencies", "ListExchanges", "ListNotes", "GetNote",
        ]);
        functions.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Description));
        functions.Select(f => f.JsonSchema.GetRawText()).Should().NotContain(s => s.Contains("userId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetTools_InvokingAFunction_RunsTheTool()
    {
        AIFunction value = _tools.GetTools().Single(f => f.Name == "GetPortfolioValue");

        object? result = await value.InvokeAsync(new AIFunctionArguments { ["portfolioId"] = PortfolioId }, Token);

        result.Should().NotBeNull();
        result!.ToString().Should().Contain((await _sk.GetPortfolioValueAsync(PortfolioId, Token))!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void HistoryReturns(PortfolioHistoryResponse response) =>
        _sender.Send(Arg.Any<GetPortfolioHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PortfolioHistoryResponse>>(response));
}
