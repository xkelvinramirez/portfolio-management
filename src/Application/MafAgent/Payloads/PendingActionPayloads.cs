using System.Reflection;
using Domain.Entities;

namespace Application.MafAgent.Payloads;

// Typed payload of each pending action (plan §0 "Payloads tipados"). Edits carry the id of the resource
// they edit, marked with [PayloadTarget], plus EVERY field of the full update request the existing
// command expects, already merged with the current values (plan §0 "Ediciones = petición completa").
// All dates are UTC instants (Kind=Utc); PendingActionFactory rejects anything else.

public sealed record CreatePortfolioPayload(string Name, string Description);

public sealed record UpdatePortfolioPayload(
    [property: PayloadTarget] long PortfolioId,
    string Name,
    string Description);

public sealed record CreateEntryPayload(
    long PortfolioId,
    long CryptoCurrencyId,
    long ExchangeId,
    decimal Quantity,
    decimal PricePerUnit,
    DateTime RecordedAt);

public sealed record UpdateEntryPayload(
    [property: PayloadTarget] long EntryId,
    long CryptoCurrencyId,
    long ExchangeId,
    decimal Quantity,
    decimal PricePerUnit,
    DateTime RecordedAt);

public sealed record CreateNotePayload(string Category, string Title, string Content);

public sealed record UpdateNotePayload(
    [property: PayloadTarget] string NoteId,
    string Category,
    string Title,
    string Content,
    bool IsActive);

public sealed record RefreshPricesPayload(long PortfolioId);

/// <summary>The period is always concrete: when the user gives none it is resolved when proposing.</summary>
public sealed record SendStatusReportPayload(long PortfolioId, DateTime PeriodStart, DateTime PeriodEnd);

/// <summary>Marks the id of the resource an edit payload targets. It is not a field of the update request.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PayloadTargetAttribute : Attribute;

public static class PendingActionPayloads
{
    public static Type TypeOf(PendingActionOperation op) => op switch
    {
        PendingActionOperation.CreatePortfolio => typeof(CreatePortfolioPayload),
        PendingActionOperation.UpdatePortfolio => typeof(UpdatePortfolioPayload),
        PendingActionOperation.CreateEntry => typeof(CreateEntryPayload),
        PendingActionOperation.UpdateEntry => typeof(UpdateEntryPayload),
        PendingActionOperation.CreateNote => typeof(CreateNotePayload),
        PendingActionOperation.UpdateNote => typeof(UpdateNotePayload),
        PendingActionOperation.RefreshPrices => typeof(RefreshPricesPayload),
        PendingActionOperation.SendStatusReport => typeof(SendStatusReportPayload),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown pending action operation."),
    };

    public static bool IsEdit(PendingActionOperation op) =>
        op is PendingActionOperation.UpdatePortfolio or PendingActionOperation.UpdateEntry or PendingActionOperation.UpdateNote;

    /// <summary>The id of the edited resource, as the string <c>ICurrentValuesReader</c> takes; null for non-edits.</summary>
    public static string? TargetIdOf(object payload)
    {
        PropertyInfo? target = Properties(payload.GetType()).SingleOrDefault(IsTarget);
        return target is null ? null : FieldSnapshot.Format(target.GetValue(payload));
    }

    internal static IEnumerable<PropertyInfo> Properties(Type payloadType) =>
        payloadType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract");

    internal static bool IsTarget(PropertyInfo property) =>
        property.GetCustomAttribute<PayloadTargetAttribute>() is not null;
}
