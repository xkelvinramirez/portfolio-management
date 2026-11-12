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
using MediatR;

namespace Application.MafAgent;

/// <summary>Outcome of a confirmed SendStatusReport: the report for this period was sent.</summary>
public sealed record StatusReportSentResult(long PortfolioId, DateTime PeriodStart, DateTime PeriodEnd);

/// <summary>
/// Runs a claimed pending action through the EXISTING command (plan §3 "Ejecutor"), so each write keeps its
/// own validation, ownership checks, cache invalidation and commit. A command error comes back as an error.
/// </summary>
public sealed class PendingActionExecutor(
    ISender sender,
    IAddedEntityIdLog addedEntityIdLog,
    PendingActionFactory factory)
{
    /// <param name="ct">
    /// Deliberately NOT forwarded: commands always run with <see cref="CancellationToken.None"/>, so a client
    /// disconnect cannot cut a write that the claim already made irreversible (plan §0 "Tras el reclamo").
    /// </param>
    /// <exception cref="InvalidOperationException">A create succeeded but no database id was recorded (wiring error).</exception>
    public async Task<ErrorOr<object>> ExecuteAsync(PendingAction action, CancellationToken ct)
    {
        CancellationToken none = CancellationToken.None;

        switch (factory.ReadPayload(action))
        {
            case CreatePortfolioPayload p:
                {
                    ErrorOr<CreatePortfolioResponse> created = await sender.Send(
                        new CreatePortfolioCommand(new CreatePortfolioRequest { Name = p.Name, Description = p.Description }), none);
                    return created.IsError ? created.Errors : created.Value with { Id = AddedId<Portfolio>() };
                }

            case UpdatePortfolioPayload p:
                return Box(await sender.Send(
                    new UpdatePortfolioCommand(p.PortfolioId, new UpdatePortfolioRequest { Name = p.Name, Description = p.Description }), none));

            case CreateEntryPayload p:
                {
                    ErrorOr<CreatePortfolioEntryResponse> created = await sender.Send(
                        new CreatePortfolioEntryCommand(new CreatePortfolioEntryRequest
                        {
                            PortfolioId = p.PortfolioId,
                            CryptoCurrencyId = p.CryptoCurrencyId,
                            ExchangeId = p.ExchangeId,
                            Quantity = p.Quantity,
                            PricePerUnit = p.PricePerUnit,
                            RecordedAt = p.RecordedAt,
                        }),
                        none);
                    return created.IsError ? created.Errors : created.Value with { Id = AddedId<PortfolioEntry>() };
                }

            case UpdateEntryPayload p:
                return Box(await sender.Send(
                    new UpdatePortfolioEntryCommand(p.EntryId, new UpdatePortfolioEntryRequest
                    {
                        CryptoCurrencyId = p.CryptoCurrencyId,
                        ExchangeId = p.ExchangeId,
                        Quantity = p.Quantity,
                        PricePerUnit = p.PricePerUnit,
                        RecordedAt = p.RecordedAt,
                    }),
                    none));

            // The note id is a Guid generated in Domain before saving, so the response already carries it.
            case CreateNotePayload p:
                return Box(await sender.Send(
                    new CreateNoteCommand(new CreateNoteRequest { Category = p.Category, Title = p.Title, Content = p.Content, IsActive = true }),
                    none));

            case UpdateNotePayload p:
                return Box(await sender.Send(
                    new UpdateNoteCommand(p.NoteId, new UpdateNoteRequest
                    {
                        Category = p.Category,
                        Title = p.Title,
                        Content = p.Content,
                        IsActive = p.IsActive,
                    }),
                    none));

            case RefreshPricesPayload p:
                return Box(await sender.Send(new RefreshPortfolioPricesCommand(p.PortfolioId), none));

            case SendStatusReportPayload p:
                {
                    ErrorOr<Success> sent = await sender.Send(
                        new TriggerPortfolioStatusReportCommand(p.PortfolioId, p.PeriodStart, p.PeriodEnd), none);
                    return sent.IsError ? sent.Errors : new StatusReportSentResult(p.PortfolioId, p.PeriodStart, p.PeriodEnd);
                }

            default:
                throw new InvalidOperationException($"No command for pending action operation {action.Operation}.");
        }
    }

    // Create handlers build their response before the unit of work commits, so its Id is still 0 (analyze G2).
    private long AddedId<TEntity>() where TEntity : Domain.Common.Entity =>
        addedEntityIdLog.LastAddedId<TEntity>()
        ?? throw new InvalidOperationException($"The {typeof(TEntity).Name} was created but no database id was recorded.");

    private static ErrorOr<object> Box<T>(ErrorOr<T> result) => result.IsError ? result.Errors : result.Value!;
}
