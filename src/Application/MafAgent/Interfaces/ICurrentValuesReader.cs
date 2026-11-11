using Domain.Entities;
using ErrorOr;

namespace Application.MafAgent.Interfaces;

/// <summary>
/// Reads the current values of an editable resource as a field snapshot (field → invariant string:
/// decimals "G29", dates round-trip "O" UTC, booleans "true"/"false"). The snapshot holds exactly the
/// fields of the full update request. Used to build Before when proposing and to detect staleness when
/// confirming (spec AC18).
/// </summary>
public interface ICurrentValuesReader
{
    Task<ErrorOr<IReadOnlyDictionary<string, string?>>> ReadAsync(
        PendingActionOperation op, string targetId, CancellationToken ct);
}
