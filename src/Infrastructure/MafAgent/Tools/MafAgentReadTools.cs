using System.ComponentModel;
using System.Globalization;
using Application.Common.Security;
using Application.CryptoCurrencies.Query;
using Application.Exchanges.Query;
using Application.Notes.Query;
using Application.PortfolioEntries.Interfaces;
using Application.PortfolioEntries.Query;
using Application.Portfolios.Interfaces;
using Application.Portfolios.Query;
using Contracts.Common;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.AI;

namespace Infrastructure.MafAgent.Tools;

internal sealed record MafPortfolioSummary(long Id, string Name, string Description);

internal sealed record MafHolding(string Symbol, decimal Quantity, decimal Value);

internal sealed record MafAssetValuePoint(string Symbol, DateTime Date, decimal Value);

internal sealed record MafAssetValueComparison(string Symbol, decimal StartValue, decimal EndValue, decimal Difference);

internal sealed record MafEntry(long Id, long CryptoCurrencyId, long ExchangeId, decimal Quantity, decimal PricePerUnit, DateTime RecordedAt);

internal sealed record MafHistoryPoint(DateTime Date, decimal Value);

internal sealed record MafAllocation(string Label, decimal Value, decimal Percentage);

internal sealed record MafCryptoCurrency(long Id, string Symbol, string Name);

// Id and name only: an exchange's API key never reaches the model.
internal sealed record MafExchange(long Id, string Name);

internal sealed record MafNoteSummary(string Id, string Category, string Title, bool IsActive, DateTime CreatedAt);

internal sealed record MafNote(string Id, string Category, string Title, string Content, bool IsActive, DateTime CreatedAt);

/// <summary>
/// The agent's read tools (plan §3 "Herramientas", T026). None has side effects and none takes a user id:
/// identity always comes from <see cref="ICurrentUserProvider"/>. Expected failures are returned as text the
/// model can act on (<see cref="NotFound"/>, <see cref="InvalidDates"/>), never thrown.
/// <para>
/// Parity (AC1): the five tools inherited from the Semantic Kernel agent use the same sources and rules as
/// <c>Infrastructure.Agent.Plugins.PortfolioAgentPlugin</c> (repositories plus the Domain valuation methods).
/// That code is replicated, not shared, because the SK agent must not change (AC11); a test pins the parity.
/// The other read tools go through the existing queries, which already check ownership.
/// </para>
/// </summary>
internal sealed class MafAgentReadTools(
    IPortfolioRepository portfolioRepository,
    IPortfolioEntryRepository portfolioEntryRepository,
    ISender sender,
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider)
{
    public const string NotFound = "NotFound";
    public const string InvalidDates = "InvalidDates";

    private const string DateFormat = "yyyy-MM-dd";
    private const int PageLimit = 100;
    private const string PortfolioIdHint = "The portfolio id, as returned by ListMyPortfolios";

    public IReadOnlyList<AIFunction> GetTools() =>
    [
        Tool(ListMyPortfolios),
        Tool(GetPortfolioValue),
        Tool(GetPortfolioHoldings),
        Tool(GetAssetPeakOrTroughDate),
        Tool(GetPortfolioAssetValueComparison),
        Tool(GetPortfolioEntries),
        Tool(GetPortfolioHistory),
        Tool(GetPortfolioAllocation),
        Tool(ListCryptoCurrencies),
        Tool(ListExchanges),
        Tool(ListNotes),
        Tool(GetNote),
    ];

    // ---- inherited from the SK agent (parity) ----

    [Description("Lists the authenticated user's own portfolios (id, name, description).")]
    public async Task<object> ListMyPortfolios(CancellationToken cancellationToken = default)
    {
        long userId = currentUserProvider.UserId;
        PaginatorResponse<Portfolio> page = await portfolioRepository.GetAllAsync(
            page: 1, limit: 50, predicate: p => p.UserId == userId, cancellationToken);

        return page.Data.Select(p => new MafPortfolioSummary(p.Id, p.Name, p.Description)).ToList();
    }

    [Description("Gets the current total value (sum of ALL holdings combined) of one of the user's portfolios. Use ONLY when the question has no specific asset symbol and no date. For a per-asset breakdown use GetPortfolioHoldings; to compare asset values across two dates use GetPortfolioAssetValueComparison; for one asset's historical peak or trough use GetAssetPeakOrTroughDate.")]
    public async Task<object> GetPortfolioValue(
        [Description(PortfolioIdHint)] long portfolioId,
        CancellationToken cancellationToken = default)
    {
        Portfolio? portfolio = await GetOwnedPortfolioOrNullAsync(portfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound;
        }

        List<PortfolioEntry> entries = await portfolioEntryRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        return portfolio.GetTotalValue(entries);
    }

    [Description("Gets the CURRENT per-asset breakdown (symbol, quantity, value) of one of the user's portfolios, ordered from highest to lowest value. Do NOT use it for historical dates.")]
    public async Task<object> GetPortfolioHoldings(
        [Description(PortfolioIdHint)] long portfolioId,
        CancellationToken cancellationToken = default)
    {
        Portfolio? portfolio = await GetOwnedPortfolioOrNullAsync(portfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound;
        }

        List<PortfolioEntry> entries = await portfolioEntryRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        return portfolio.GetHoldingsAsOf(entries, timeProvider.GetUtcNow().UtcDateTime)
            .Select(e => new MafHolding(e.CryptoCurrency.Symbol, e.Quantity, e.Quantity * e.PricePerUnit))
            .OrderByDescending(h => h.Value)
            .ToList();
    }

    [Description("Finds the DATE when ONE asset (by symbol) reached its historical highest or lowest value within one of the user's portfolios, optionally limited to a date range. For comparing asset values across two dates use GetPortfolioAssetValueComparison.")]
    public async Task<object> GetAssetPeakOrTroughDate(
        [Description(PortfolioIdHint)] long portfolioId,
        [Description("The asset symbol, e.g. BTC")] string symbol,
        [Description("'max' for the highest value, 'min' for the lowest value")] string extreme,
        [Description("Optional start date (inclusive, yyyy-MM-dd)")] string? from = null,
        [Description("Optional end date (inclusive, yyyy-MM-dd)")] string? to = null,
        CancellationToken cancellationToken = default)
    {
        Portfolio? portfolio = await GetOwnedPortfolioOrNullAsync(portfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound;
        }

        List<PortfolioEntry> entries = await portfolioEntryRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        PortfolioAssetValuationSeries? series = portfolio.GetValueHistoryByAsset(entries)
            .FirstOrDefault(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        IEnumerable<PortfolioValuationPoint> points = series?.Points.AsEnumerable() ?? [];
        // Same rule as the SK plugin: an unparseable optional bound is ignored, not an error.
        if (DateTime.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fromDate))
        {
            points = points.Where(p => p.Date >= fromDate.Date);
        }

        if (DateTime.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime toDate))
        {
            points = points.Where(p => p.Date <= toDate.Date);
        }

        List<PortfolioValuationPoint> pointList = points.ToList();
        if (pointList.Count == 0)
        {
            return NotFound;
        }

        // Anything other than an exact "min" is "max", as in the SK plugin.
        bool wantsMin = string.Equals(extreme.Trim(), "min", StringComparison.OrdinalIgnoreCase);
        PortfolioValuationPoint extremePoint = wantsMin ? pointList.MinBy(p => p.Value)! : pointList.MaxBy(p => p.Value)!;
        return new MafAssetValuePoint(series!.Symbol, extremePoint.Date, extremePoint.Value);
    }

    [Description("Compares the value of every asset in one of the user's portfolios between two dates (yyyy-MM-dd). Returns each asset's value on or before each date and the signed difference (end minus start), including assets present on only one date. Returns InvalidDates for a bad format or a start after the end.")]
    public async Task<object> GetPortfolioAssetValueComparison(
        [Description(PortfolioIdHint)] long portfolioId,
        [Description("The inclusive start date, yyyy-MM-dd")] string from,
        [Description("The inclusive end date, yyyy-MM-dd")] string to,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseDate(from, out DateTime fromDate) || !TryParseDate(to, out DateTime toDate) || fromDate > toDate)
        {
            return InvalidDates;
        }

        Portfolio? portfolio = await GetOwnedPortfolioOrNullAsync(portfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound;
        }

        List<PortfolioEntry> entries = await portfolioEntryRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        Dictionary<string, decimal> startValues = ValuesBySymbol(portfolio.GetHoldingsAsOf(entries, fromDate));
        Dictionary<string, decimal> endValues = ValuesBySymbol(portfolio.GetHoldingsAsOf(entries, toDate));

        return startValues.Keys
            .Union(endValues.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(symbol => symbol, StringComparer.OrdinalIgnoreCase)
            .Select(symbol =>
            {
                decimal startValue = startValues.GetValueOrDefault(symbol);
                decimal endValue = endValues.GetValueOrDefault(symbol);
                return new MafAssetValueComparison(symbol, startValue, endValue, endValue - startValue);
            })
            .ToList();
    }

    // ---- through the existing queries ----

    [Description("Lists every entry (id, asset id, exchange id, quantity, price per unit, recorded at UTC) of one of the user's portfolios.")]
    public async Task<object> GetPortfolioEntries(
        [Description(PortfolioIdHint)] long portfolioId,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<List<MafEntry>> entries = await ReadAllPagesAsync(
            paginator => sender.Send(new GetPortfolioEntriesQuery(portfolioId, paginator), cancellationToken),
            e => new MafEntry(e.Id, e.CryptoCurrencyId, e.ExchangeId, e.Quantity, e.PricePerUnit, e.RecordedAt));
        return entries.IsError ? NotFound : entries.Value;
    }

    [Description("Gets the daily total value history of one of the user's portfolios, optionally limited to an inclusive range (yyyy-MM-dd). Returns InvalidDates for a bad format or a start after the end.")]
    public async Task<object> GetPortfolioHistory(
        [Description(PortfolioIdHint)] long portfolioId,
        [Description("Optional start date (inclusive, yyyy-MM-dd)")] string? from = null,
        [Description("Optional end date (inclusive, yyyy-MM-dd)")] string? to = null,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDate = DateTime.MinValue;
        DateTime toDate = DateTime.MaxValue;
        if ((from is not null && !TryParseDate(from, out fromDate)) ||
            (to is not null && !TryParseDate(to, out toDate)) ||
            fromDate > toDate)
        {
            return InvalidDates;
        }

        ErrorOr<PortfolioHistoryResponse> history = await sender.Send(new GetPortfolioHistoryQuery(portfolioId), cancellationToken);
        if (history.IsError)
        {
            return NotFound;
        }

        return history.Value.Points
            .Where(p => p.Date.Date >= fromDate && p.Date.Date <= toDate)
            .Select(p => new MafHistoryPoint(p.Date, p.Value))
            .ToList();
    }

    [Description("Gets today's allocation by asset (label, value, percentage) of one of the user's portfolios.")]
    public async Task<object> GetPortfolioAllocation(
        [Description(PortfolioIdHint)] long portfolioId,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<PortfolioAllocationResponse> allocation = await sender.Send(
            new GetPortfolioAllocationQuery(portfolioId, PortfolioAllocationGroupBy.Asset, null), cancellationToken);
        if (allocation.IsError)
        {
            return NotFound;
        }

        return allocation.Value.Items.Select(i => new MafAllocation(i.Label, i.Value, i.Percentage)).ToList();
    }

    [Description("Lists the cryptocurrency catalog (id, symbol, name).")]
    public async Task<object> ListCryptoCurrencies(CancellationToken cancellationToken = default)
    {
        ErrorOr<List<MafCryptoCurrency>> items = await ReadAllPagesAsync(
            paginator => sender.Send(new GetCryptoCurrenciesQuery(paginator), cancellationToken),
            c => new MafCryptoCurrency(c.Id, c.Symbol, c.Name));
        return items.IsError ? NotFound : items.Value;
    }

    [Description("Lists the exchange catalog (id, name).")]
    public async Task<object> ListExchanges(CancellationToken cancellationToken = default)
    {
        ErrorOr<List<MafExchange>> items = await ReadAllPagesAsync(
            paginator => sender.Send(new GetExchangesQuery(paginator), cancellationToken),
            e => new MafExchange(e.Id, e.Name));
        return items.IsError ? NotFound : items.Value;
    }

    [Description("Lists the notes (id, category, title, active flag, created at) without their content. Use GetNote to read one note's content.")]
    public async Task<object> ListNotes(CancellationToken cancellationToken = default)
    {
        ErrorOr<List<MafNoteSummary>> notes = await ReadAllPagesAsync(
            paginator => sender.Send(new GetNotesQuery(paginator, null, null), cancellationToken),
            n => new MafNoteSummary(n.Id, n.Category, n.Title, n.IsActive, n.CreatedAt));
        return notes.IsError ? NotFound : notes.Value;
    }

    [Description("Gets one note, including its content, by id (as returned by ListNotes).")]
    public async Task<object> GetNote(
        [Description("The note id, as returned by ListNotes")] string noteId,
        CancellationToken cancellationToken = default)
    {
        ErrorOr<Contracts.Notes.NoteResponse> note = await sender.Send(new GetNoteByIdQuery(noteId), cancellationToken);
        if (note.IsError)
        {
            return NotFound;
        }

        return new MafNote(note.Value.Id, note.Value.Category, note.Value.Title, note.Value.Content, note.Value.IsActive, note.Value.CreatedAt);
    }

    private static AIFunction Tool(Delegate method) =>
        AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { Name = method.Method.Name });

    private static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static Dictionary<string, decimal> ValuesBySymbol(IEnumerable<PortfolioEntry> holdings) =>
        holdings
            .GroupBy(entry => entry.CryptoCurrency.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => entry.Quantity * entry.PricePerUnit),
                StringComparer.OrdinalIgnoreCase);

    private static async Task<ErrorOr<List<TOut>>> ReadAllPagesAsync<TIn, TOut>(
        Func<PaginatorRequest, Task<ErrorOr<PaginatorResponse<TIn>>>> readPage, Func<TIn, TOut> map)
    {
        List<TOut> all = [];
        int page = 1;
        int totalPages;
        do
        {
            ErrorOr<PaginatorResponse<TIn>> result = await readPage(new PaginatorRequest(page, PageLimit));
            if (result.IsError)
            {
                return result.Errors;
            }

            all.AddRange(result.Value.Data.Select(map));
            totalPages = result.Value.TotalPages;
            page++;
        }
        while (page <= totalPages);

        return all;
    }

    // Never trust a model-supplied id: the portfolio must belong to the authenticated user.
    private async Task<Portfolio?> GetOwnedPortfolioOrNullAsync(long portfolioId, CancellationToken cancellationToken)
    {
        Portfolio? portfolio = await portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
        return portfolio is not null && portfolio.UserId == currentUserProvider.UserId ? portfolio : null;
    }
}
