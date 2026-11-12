using System.ComponentModel;
using System.Globalization;
using Application.CryptoCurrencies.Query;
using Application.Exchanges.Query;
using Application.MafAgent;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Application.Portfolios.Query;
using Application.Reports;
using Contracts.MafAgent;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.AI;

namespace Infrastructure.MafAgent.Tools;

/// <summary>What a propose tool returns to the model: the staged action, never its execution.</summary>
internal sealed record MafPendingActionSummary(
    Guid Id,
    string Operation,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, string?>? Before,
    string ExpiresAt);

/// <summary>
/// The agent's propose tools (plan §3 "Herramientas", T028). They NEVER write business data: they validate,
/// resolve the catalog, build the full request the existing command expects and stage ONE pending action
/// per call with <see cref="IPendingActionRepository.Add"/>. Only queries are sent. The write happens only
/// when the user confirms the action by id (AC2, AC4).
/// <para>
/// Expected failures are returned as text for the model: <c>MissingOrAmbiguous:&lt;field&gt;</c> (ask for it,
/// AC9), <c>NotFound</c> and <c>UnknownCatalogItem:&lt;value&gt;</c>. In those cases nothing is staged.
/// </para>
/// <para>
/// Dates arrive as TEXT on purpose, a deviation from plan §0 "Payloads tipados" (which says DateTimeOffset):
/// System.Text.Json would read a date without offset in the SERVER's time zone, while the rule is that it
/// means Europe/Madrid. With Z or an offset the instant is kept; without one it is Madrid local time.
/// </para>
/// </summary>
internal sealed class MafAgentProposeTools(
    PendingActionFactory factory,
    IPendingActionRepository pendingActionRepository,
    ICurrentValuesReader currentValuesReader,
    ISender sender,
    TimeProvider timeProvider)
{
    public const string NotFound = "NotFound";

    private const string DateHint = "ISO-8601 instant. Prefer UTC with Z (2026-11-10T18:30:00Z); without an offset it is read as Europe/Madrid local time";

    private readonly List<PendingAction> _proposed = [];
    private Guid? _conversationId;

    /// <summary>The actions staged during the current turn: the same instances given to the repository.</summary>
    public IReadOnlyList<PendingAction> ProposedActions => _proposed;

    /// <summary>Called by the runner before each turn: actions belong to this conversation, and the list restarts.</summary>
    public void BeginTurn(Guid conversationId)
    {
        _conversationId = conversationId;
        _proposed.Clear();
    }

    public IReadOnlyList<AIFunction> GetTools() =>
    [
        Tool(ProposeCreatePortfolio),
        Tool(ProposeUpdatePortfolio),
        Tool(ProposeCreateEntry),
        Tool(ProposeUpdateEntry),
        Tool(ProposeCreateNote),
        Tool(ProposeUpdateNote),
        Tool(ProposeRefreshPrices),
        Tool(ProposeSendStatusReport),
    ];

    // ---- portfolios ----

    [Description("Proposes creating a portfolio. Nothing is written until the user confirms the returned pending action.")]
    public Task<object> ProposeCreatePortfolio(
        [Description("The portfolio name")] string? name,
        [Description("Optional description")] string? description,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(name))
        {
            return Task.FromResult<object>(Missing("name"));
        }

        return Task.FromResult<object>(Stage(PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload(name!.Trim(), description?.Trim() ?? ""), null));
    }

    [Description("Proposes editing one of the user's portfolios. Pass ONLY the fields that change. Nothing is written until the user confirms.")]
    public async Task<object> ProposeUpdatePortfolio(
        [Description("The portfolio id, as returned by ListMyPortfolios")] long? portfolioId,
        [Description("New name, only if it changes")] string? name = null,
        [Description("New description, only if it changes")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (portfolioId is null)
        {
            return Missing("portfolioId");
        }

        ErrorOr<IReadOnlyDictionary<string, string?>> current = await ReadCurrentAsync(PendingActionOperation.UpdatePortfolio, portfolioId.Value, cancellationToken);
        if (current.IsError)
        {
            return NotFound;
        }

        UpdatePortfolioPayload payload = new(
            portfolioId.Value,
            IsBlank(name) ? current.Value["Name"]! : name!.Trim(),
            IsBlank(description) ? current.Value["Description"] ?? "" : description!.Trim());
        return StageEdit(PendingActionOperation.UpdatePortfolio, payload, current.Value);
    }

    // ---- entries ----

    [Description("Proposes recording a buy/holding entry in one of the user's portfolios. Every argument is required: ask the user for any that is missing. Nothing is written until the user confirms.")]
    public async Task<object> ProposeCreateEntry(
        [Description("The portfolio id, as returned by ListMyPortfolios")] long? portfolioId,
        [Description("The asset symbol, e.g. BTC")] string? symbol,
        [Description("The exchange name, e.g. Binance")] string? exchangeName,
        [Description("The quantity, greater than 0")] decimal? quantity,
        [Description("The price per unit, 0 or greater")] decimal? pricePerUnit,
        [Description("When it was recorded. " + DateHint)] string? recordedAt,
        CancellationToken cancellationToken = default)
    {
        if (portfolioId is null)
        {
            return Missing("portfolioId");
        }

        if (IsBlank(symbol))
        {
            return Missing("symbol");
        }

        if (IsBlank(exchangeName))
        {
            return Missing("exchangeName");
        }

        if (quantity is null or <= 0)
        {
            return Missing("quantity");
        }

        if (pricePerUnit is null or < 0)
        {
            return Missing("pricePerUnit");
        }

        if (!TryParseInstant(recordedAt, out DateTime recordedAtUtc))
        {
            return Missing("recordedAt");
        }

        ErrorOr<PortfolioResponse> portfolio = await sender.Send(new GetPortfolioByIdQuery(portfolioId.Value), cancellationToken);
        if (portfolio.IsError)
        {
            return NotFound;
        }

        CatalogMatch crypto = await ResolveCryptoAsync(symbol!, cancellationToken);
        if (crypto.Failure is not null)
        {
            return crypto.Failure;
        }

        CatalogMatch exchange = await ResolveExchangeAsync(exchangeName!, cancellationToken);
        if (exchange.Failure is not null)
        {
            return exchange.Failure;
        }

        return Stage(
            PendingActionOperation.CreateEntry,
            new CreateEntryPayload(portfolioId.Value, crypto.Id, exchange.Id, quantity.Value, pricePerUnit.Value, recordedAtUtc),
            null);
    }

    [Description("Proposes editing one of the user's portfolio entries. Pass ONLY the fields that change. Nothing is written until the user confirms.")]
    public async Task<object> ProposeUpdateEntry(
        [Description("The entry id, as returned by GetPortfolioEntries")] long? entryId,
        [Description("New quantity, only if it changes (greater than 0)")] decimal? quantity = null,
        [Description("New price per unit, only if it changes (0 or greater)")] decimal? pricePerUnit = null,
        [Description("New recorded date, only if it changes. " + DateHint)] string? recordedAt = null,
        [Description("New asset symbol, only if it changes")] string? symbol = null,
        [Description("New exchange name, only if it changes")] string? exchangeName = null,
        CancellationToken cancellationToken = default)
    {
        if (entryId is null)
        {
            return Missing("entryId");
        }

        if (quantity is <= 0)
        {
            return Missing("quantity");
        }

        if (pricePerUnit is < 0)
        {
            return Missing("pricePerUnit");
        }

        DateTime? newRecordedAt = null;
        if (!IsBlank(recordedAt))
        {
            if (!TryParseInstant(recordedAt, out DateTime parsed))
            {
                return Missing("recordedAt");
            }

            newRecordedAt = parsed;
        }

        ErrorOr<IReadOnlyDictionary<string, string?>> current = await ReadCurrentAsync(PendingActionOperation.UpdateEntry, entryId.Value, cancellationToken);
        if (current.IsError)
        {
            return NotFound;
        }

        long cryptoId = ParseLong(current.Value["CryptoCurrencyId"]);
        if (!IsBlank(symbol))
        {
            CatalogMatch crypto = await ResolveCryptoAsync(symbol!, cancellationToken);
            if (crypto.Failure is not null)
            {
                return crypto.Failure;
            }

            cryptoId = crypto.Id;
        }

        long exchangeId = ParseLong(current.Value["ExchangeId"]);
        if (!IsBlank(exchangeName))
        {
            CatalogMatch exchange = await ResolveExchangeAsync(exchangeName!, cancellationToken);
            if (exchange.Failure is not null)
            {
                return exchange.Failure;
            }

            exchangeId = exchange.Id;
        }

        UpdateEntryPayload payload = new(
            entryId.Value,
            cryptoId,
            exchangeId,
            quantity ?? ParseDecimal(current.Value["Quantity"]),
            pricePerUnit ?? ParseDecimal(current.Value["PricePerUnit"]),
            newRecordedAt ?? ParseInstant(current.Value["RecordedAt"]));
        return StageEdit(PendingActionOperation.UpdateEntry, payload, current.Value);
    }

    // ---- notes ----

    [Description("Proposes creating a note. Category, title and content are required. Nothing is written until the user confirms.")]
    public Task<object> ProposeCreateNote(
        [Description("The note category")] string? category,
        [Description("The note title")] string? title,
        [Description("The note content")] string? content,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(category))
        {
            return Task.FromResult<object>(Missing("category"));
        }

        if (IsBlank(title))
        {
            return Task.FromResult<object>(Missing("title"));
        }

        if (IsBlank(content))
        {
            return Task.FromResult<object>(Missing("content"));
        }

        return Task.FromResult<object>(Stage(PendingActionOperation.CreateNote, new CreateNotePayload(category!.Trim(), title!.Trim(), content!), null));
    }

    [Description("Proposes editing a note. Pass ONLY the fields that change; the note keeps its active state. Nothing is written until the user confirms.")]
    public async Task<object> ProposeUpdateNote(
        [Description("The note id, as returned by ListNotes")] string? noteId,
        [Description("New category, only if it changes")] string? category = null,
        [Description("New title, only if it changes")] string? title = null,
        [Description("New content, only if it changes")] string? content = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(noteId))
        {
            return Missing("noteId");
        }

        ErrorOr<IReadOnlyDictionary<string, string?>> current = await currentValuesReader.ReadAsync(
            PendingActionOperation.UpdateNote, noteId!, cancellationToken);
        if (current.IsError)
        {
            return NotFound;
        }

        UpdateNotePayload payload = new(
            noteId!,
            IsBlank(category) ? current.Value["Category"]! : category!.Trim(),
            IsBlank(title) ? current.Value["Title"]! : title!.Trim(),
            IsBlank(content) ? current.Value["Content"]! : content!,
            bool.Parse(current.Value["IsActive"]!));
        return StageEdit(PendingActionOperation.UpdateNote, payload, current.Value);
    }

    // ---- system actions ----

    [Description("Proposes refreshing the current prices of one of the user's portfolios. Nothing runs until the user confirms.")]
    public async Task<object> ProposeRefreshPrices(
        [Description("The portfolio id, as returned by ListMyPortfolios")] long? portfolioId,
        CancellationToken cancellationToken = default)
    {
        if (portfolioId is null)
        {
            return Missing("portfolioId");
        }

        ErrorOr<PortfolioResponse> portfolio = await sender.Send(new GetPortfolioByIdQuery(portfolioId.Value), cancellationToken);
        return portfolio.IsError ? NotFound : Stage(PendingActionOperation.RefreshPrices, new RefreshPricesPayload(portfolioId.Value), null);
    }

    [Description("Proposes sending the status report of one of the user's portfolios by e-mail. Without a period it covers the previous calendar month. Nothing is sent until the user confirms.")]
    public async Task<object> ProposeSendStatusReport(
        [Description("The portfolio id, as returned by ListMyPortfolios")] long? portfolioId,
        [Description("Optional period start, together with periodEnd. " + DateHint)] string? periodStart = null,
        [Description("Optional period end, together with periodStart. " + DateHint)] string? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (portfolioId is null)
        {
            return Missing("portfolioId");
        }

        DateTime start;
        DateTime end;
        if (IsBlank(periodStart) && IsBlank(periodEnd))
        {
            // Resolved NOW, with the same function the report handler uses: the payload never has a null period.
            (start, end) = PortfolioStatusReportPeriod.PreviousCalendarMonth(timeProvider.GetUtcNow().UtcDateTime);
        }
        else if (!TryParseInstant(periodStart, out start) || !TryParseInstant(periodEnd, out end) || start > end)
        {
            return Missing("period");
        }

        ErrorOr<PortfolioResponse> portfolio = await sender.Send(new GetPortfolioByIdQuery(portfolioId.Value), cancellationToken);
        return portfolio.IsError ? NotFound : Stage(PendingActionOperation.SendStatusReport, new SendStatusReportPayload(portfolioId.Value, start, end), null);
    }

    // ---- staging ----

    private object StageEdit(PendingActionOperation op, object payload, IReadOnlyDictionary<string, string?> before)
    {
        IReadOnlyDictionary<string, string?> requested = FieldSnapshot.OfRequestFields(payload);
        bool changes = requested.Any(field => !before.TryGetValue(field.Key, out string? value) || !string.Equals(value, field.Value, StringComparison.Ordinal));
        return changes ? Stage(op, payload, before) : Missing("changes");
    }

    private object Stage(PendingActionOperation op, object payload, IReadOnlyDictionary<string, string?>? before)
    {
        Guid conversationId = _conversationId
            ?? throw new InvalidOperationException("BeginTurn must be called before any propose tool runs.");

        PendingAction action = factory.Create(conversationId, op, payload, before);
        pendingActionRepository.Add(action);
        _proposed.Add(action);

        PendingActionDto dto = factory.ToDto(action);
        return new MafPendingActionSummary(dto.Id, dto.Operation, dto.Values, dto.Before, dto.ExpiresAt);
    }

    private Task<ErrorOr<IReadOnlyDictionary<string, string?>>> ReadCurrentAsync(PendingActionOperation op, long id, CancellationToken cancellationToken) =>
        currentValuesReader.ReadAsync(op, id.ToString(CultureInfo.InvariantCulture), cancellationToken);

    // ---- catalog (plan §0 "Catálogo nombre→id") ----

    private readonly record struct CatalogMatch(long Id, string? Failure);

    private async Task<CatalogMatch> ResolveCryptoAsync(string symbol, CancellationToken cancellationToken)
    {
        string wanted = symbol.Trim();
        ErrorOr<List<(long Id, string Symbol)>> catalog = await ToolPaging.ReadAllAsync(
            paginator => sender.Send(new GetCryptoCurrenciesQuery(paginator), cancellationToken),
            c => (c.Id, c.Symbol));
        if (catalog.IsError)
        {
            throw new InvalidOperationException($"The cryptocurrency catalog could not be read: {catalog.FirstError.Code}.");
        }

        List<long> matches = catalog.Value
            .Where(c => string.Equals(c.Symbol, wanted, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .ToList();
        return Match(matches, wanted, "symbol");
    }

    private async Task<CatalogMatch> ResolveExchangeAsync(string name, CancellationToken cancellationToken)
    {
        string wanted = name.Trim();
        ErrorOr<List<(long Id, string Name)>> catalog = await ToolPaging.ReadAllAsync(
            paginator => sender.Send(new GetExchangesQuery(paginator), cancellationToken),
            e => (e.Id, e.Name));
        if (catalog.IsError)
        {
            throw new InvalidOperationException($"The exchange catalog could not be read: {catalog.FirstError.Code}.");
        }

        List<long> matches = catalog.Value
            .Where(e => string.Equals(e.Name.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id)
            .ToList();
        return Match(matches, wanted, "exchangeName");
    }

    private static CatalogMatch Match(List<long> matches, string value, string field) => matches.Count switch
    {
        0 => new CatalogMatch(0, $"UnknownCatalogItem:{value}"),
        1 => new CatalogMatch(matches[0], null),
        _ => new CatalogMatch(0, Missing(field)),
    };

    // ---- values ----

    private static string Missing(string field) => $"MissingOrAmbiguous:{field}";

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>With Z or an offset: that instant. Without an offset: Europe/Madrid local time. Always Kind=Utc.</summary>
    private static bool TryParseInstant(string? text, out DateTime utc)
    {
        utc = default;
        if (IsBlank(text) ||
            !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out DateTime parsed))
        {
            return false;
        }

        switch (parsed.Kind)
        {
            case DateTimeKind.Utc:
                utc = parsed;
                return true;
            case DateTimeKind.Local: // it carried an explicit offset
                utc = parsed.ToUniversalTime();
                return true;
            default:
                if (BusinessTime.Zone.IsInvalidTime(parsed))
                {
                    return false; // a local time skipped by the spring-forward change
                }

                utc = TimeZoneInfo.ConvertTimeToUtc(parsed, BusinessTime.Zone);
                return true;
        }
    }

    // The current values come from ICurrentValuesReader in the FieldSnapshot format ("G29", "O").
    private static long ParseLong(string? value) => long.Parse(value!, CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string? value) => decimal.Parse(value!, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static DateTime ParseInstant(string? value) =>
        DateTime.Parse(value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static AIFunction Tool(Delegate method) =>
        AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { Name = method.Method.Name });
}
