using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Application.Common.Security;
using Application.MafAgent.Payloads;
using Contracts.MafAgent;
using Domain.Entities;

namespace Application.MafAgent;

/// <summary>
/// Builds pending actions (plan T014): typed payload and, for edits, the full Before snapshot, both
/// encrypted with <see cref="IEncryptionService"/>; owner from the current user; expiry from
/// <see cref="TimeProvider"/>. Also turns an action back into what the user sees (<see cref="ToDto"/>)
/// and what the executor runs (<see cref="ReadPayload"/>). Public: the agent tools in Infrastructure use it.
/// </summary>
public sealed class PendingActionFactory(
    IEncryptionService encryptionService,
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider)
{
    /// <param name="before">Edits only: the current values as a <see cref="FieldSnapshot"/>, with exactly the request fields.</param>
    /// <exception cref="ArgumentException">Programming errors: wrong payload type, missing or extra Before, non-UTC dates.</exception>
    public PendingAction Create(
        Guid conversationId,
        PendingActionOperation op,
        object payload,
        IReadOnlyDictionary<string, string?>? before)
    {
        Validate(op, payload, before);

        string payloadJson = encryptionService.Encrypt(JsonSerializer.Serialize(payload, payload.GetType()));
        string? beforeJson = before is null ? null : encryptionService.Encrypt(JsonSerializer.Serialize(before));

        return PendingAction.Create(
            conversationId,
            currentUserProvider.UserId,
            op,
            payloadJson,
            beforeJson,
            timeProvider.GetUtcNow().UtcDateTime);
    }

    public object ReadPayload(PendingAction action) =>
        JsonSerializer.Deserialize(encryptionService.Decrypt(action.PayloadJson), PendingActionPayloads.TypeOf(action.Operation))
        ?? throw new InvalidOperationException($"Pending action {action.Id} has an empty payload.");

    public IReadOnlyDictionary<string, string?>? ReadBefore(PendingAction action) =>
        action.BeforeJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(encryptionService.Decrypt(action.BeforeJson));

    public PendingActionDto ToDto(PendingAction action)
    {
        object payload = ReadPayload(action);
        IReadOnlyDictionary<string, string?>? before = ReadBefore(action);
        PropertyInfo[] properties = PendingActionPayloads.Properties(payload.GetType()).ToArray();

        Dictionary<string, string?> values = properties.ToDictionary(p => p.Name, p => Present(p.GetValue(payload)));
        Dictionary<string, string?>? presentedBefore = before?.ToDictionary(
            field => field.Key,
            field => IsDate(properties, field.Key) && field.Value is not null
                ? BusinessTime.FormatUtc(DateTime.Parse(field.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                : field.Value);

        return new PendingActionDto(
            action.Id,
            action.Operation.ToString(),
            Summarize(action.Operation, payload, properties, before),
            values,
            presentedBefore,
            BusinessTime.FormatUtc(action.ExpiresAt));
    }

    private static void Validate(PendingActionOperation op, object payload, IReadOnlyDictionary<string, string?>? before)
    {
        Type expected = PendingActionPayloads.TypeOf(op);
        if (payload.GetType() != expected)
        {
            throw new ArgumentException($"{op} needs a {expected.Name} payload, not {payload.GetType().Name}.", nameof(payload));
        }

        DateTime[] notUtc = PendingActionPayloads.Properties(expected)
            .Select(p => p.GetValue(payload))
            .OfType<DateTime>()
            .Where(d => d.Kind != DateTimeKind.Utc)
            .ToArray();
        if (notUtc.Length > 0)
        {
            throw new ArgumentException($"Payload dates must be UTC (Kind=Utc); got {notUtc[0].Kind}.", nameof(payload));
        }

        if (!PendingActionPayloads.IsEdit(op))
        {
            if (before is not null)
            {
                throw new ArgumentException($"{op} is not an edit and takes no Before snapshot.", nameof(before));
            }

            return;
        }

        if (before is null)
        {
            throw new ArgumentException($"{op} is an edit and needs the Before snapshot.", nameof(before));
        }

        HashSet<string> requestFields = FieldSnapshot.OfRequestFields(payload).Keys.ToHashSet();
        if (!requestFields.SetEquals(before.Keys))
        {
            throw new ArgumentException(
                $"Before must hold exactly the request fields ({string.Join(", ", requestFields)}).", nameof(before));
        }
    }

    // Names the operation, its target ids, the edited fields and the dates in local time. Never repeats user
    // free text (names, titles, contents, descriptions) so it is safe to log and to show (plan §0 "Logs").
    private static string Summarize(
        PendingActionOperation op,
        object payload,
        PropertyInfo[] properties,
        IReadOnlyDictionary<string, string?>? before)
    {
        string Id(string name) => Present(properties.Single(p => p.Name == name).GetValue(payload)) ?? string.Empty;

        string label = op switch
        {
            PendingActionOperation.CreatePortfolio => "Crear portafolio",
            PendingActionOperation.UpdatePortfolio => $"Editar portafolio {Id(nameof(UpdatePortfolioPayload.PortfolioId))}",
            PendingActionOperation.CreateEntry => $"Crear entrada en el portafolio {Id(nameof(CreateEntryPayload.PortfolioId))}",
            PendingActionOperation.UpdateEntry => $"Editar entrada {Id(nameof(UpdateEntryPayload.EntryId))}",
            PendingActionOperation.CreateNote => "Crear nota",
            PendingActionOperation.UpdateNote => $"Editar nota {Id(nameof(UpdateNotePayload.NoteId))}",
            PendingActionOperation.RefreshPrices => $"Refrescar precios del portafolio {Id(nameof(RefreshPricesPayload.PortfolioId))}",
            PendingActionOperation.SendStatusReport => $"Enviar informe de estado del portafolio {Id(nameof(SendStatusReportPayload.PortfolioId))}",
            _ => op.ToString(),
        };

        var parts = new List<string> { label };
        if (before is not null)
        {
            IReadOnlyDictionary<string, string?> after = FieldSnapshot.OfRequestFields(payload);
            string[] changed = after.Where(f => before[f.Key] != f.Value).Select(f => f.Key).ToArray();
            parts.Add(changed.Length == 0 ? "sin cambios" : $"cambia {string.Join(", ", changed)}");
        }

        parts.AddRange(properties
            .Where(p => p.PropertyType == typeof(DateTime))
            .Select(p => $"{p.Name} {BusinessTime.FormatLocal((DateTime)p.GetValue(payload)!)}"));

        return string.Join("; ", parts);
    }

    private static bool IsDate(PropertyInfo[] properties, string field) =>
        properties.Any(p => p.Name == field && p.PropertyType == typeof(DateTime));

    private static string? Present(object? value) => value is DateTime date
        ? BusinessTime.FormatUtc(date)
        : FieldSnapshot.Format(value);
}
