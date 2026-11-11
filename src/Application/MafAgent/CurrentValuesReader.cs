using Application.MafAgent.Interfaces;
using Application.Notes.Query;
using Application.PortfolioEntries.Query;
using Application.Portfolios.Query;
using Contracts.Notes;
using Contracts.PortfolioEntries;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using MediatR;

namespace Application.MafAgent;

/// <summary>
/// Reads an editable resource through its existing query (same ownership rules) and returns the fields of
/// its full update request as a <see cref="FieldSnapshot"/>. Used to build Before when proposing and to
/// detect staleness when confirming. Public: the agent tools in Infrastructure use it.
/// </summary>
public sealed class CurrentValuesReader(ISender sender) : ICurrentValuesReader
{
    public async Task<ErrorOr<IReadOnlyDictionary<string, string?>>> ReadAsync(
        PendingActionOperation op, string targetId, CancellationToken ct)
    {
        switch (op)
        {
            case PendingActionOperation.UpdatePortfolio:
                {
                    if (!long.TryParse(targetId, out long id))
                    {
                        return TargetNotFound(op, targetId);
                    }

                    ErrorOr<PortfolioResponse> portfolio = await sender.Send(new GetPortfolioByIdQuery(id), ct);
                    return portfolio.IsError ? portfolio.Errors : Snapshot(new()
                    {
                        ["Name"] = portfolio.Value.Name,
                        ["Description"] = portfolio.Value.Description,
                    });
                }

            case PendingActionOperation.UpdateEntry:
                {
                    if (!long.TryParse(targetId, out long id))
                    {
                        return TargetNotFound(op, targetId);
                    }

                    ErrorOr<PortfolioEntryResponse> entry = await sender.Send(new GetPortfolioEntryByIdQuery(id), ct);
                    return entry.IsError ? entry.Errors : Snapshot(new()
                    {
                        ["CryptoCurrencyId"] = entry.Value.CryptoCurrencyId,
                        ["ExchangeId"] = entry.Value.ExchangeId,
                        ["Quantity"] = entry.Value.Quantity,
                        ["PricePerUnit"] = entry.Value.PricePerUnit,
                        ["RecordedAt"] = entry.Value.RecordedAt,
                    });
                }

            case PendingActionOperation.UpdateNote:
                {
                    ErrorOr<NoteResponse> note = await sender.Send(new GetNoteByIdQuery(targetId), ct);
                    return note.IsError ? note.Errors : Snapshot(new()
                    {
                        ["Category"] = note.Value.Category,
                        ["Title"] = note.Value.Title,
                        ["Content"] = note.Value.Content,
                        ["IsActive"] = note.Value.IsActive,
                    });
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, "Only edit operations have current values to read.");
        }
    }

    private static ErrorOr<IReadOnlyDictionary<string, string?>> Snapshot(Dictionary<string, object?> fields) =>
        fields.ToDictionary(f => f.Key, f => FieldSnapshot.Format(f.Value));

    private static Error TargetNotFound(PendingActionOperation op, string targetId) =>
        Error.NotFound("MafAgent.Target.NotFound", $"No resource '{targetId}' to {op}.");
}
