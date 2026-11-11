namespace Domain.Entities;

/// <summary>
/// A write proposed by the MAF agent that only runs after the user explicitly confirms it by id
/// (spec maf-tool-calling-agent G3/AC2–AC7). It is also the audit record of that write (AC12).
/// Payload, before-snapshot and result are stored already encrypted by the application layer.
/// Invalid transitions throw: handlers check state first, so reaching one is a programming error,
/// not a business failure.
/// </summary>
public sealed class PendingAction
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private PendingAction()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public long UserId { get; private set; }
    public PendingActionOperation Operation { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public string? BeforeJson { get; private set; }
    public PendingActionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? ResultJson { get; private set; }
    public string? ErrorCode { get; private set; }

    public static PendingAction Create(
        Guid conversationId,
        long userId,
        PendingActionOperation op,
        string payloadJson,
        string? beforeJson,
        DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(nowUtc),
            ConversationId = conversationId,
            UserId = userId,
            Operation = op,
            PayloadJson = payloadJson,
            BeforeJson = beforeJson,
            Status = PendingActionStatus.Pending,
            CreatedAt = nowUtc,
            ExpiresAt = nowUtc + Lifetime,
        };

    /// <summary>Expired from the 10-minute mark on (a claim requires ExpiresAt &gt; now).</summary>
    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    public void MarkExecuting()
    {
        EnsureStatus(PendingActionStatus.Pending, nameof(MarkExecuting));
        Status = PendingActionStatus.Executing;
    }

    public void MarkSucceeded(string resultJson, DateTime nowUtc)
    {
        EnsureStatus(PendingActionStatus.Executing, nameof(MarkSucceeded));
        Status = PendingActionStatus.Succeeded;
        ResultJson = resultJson;
        DecidedAt = nowUtc;
    }

    public void MarkFailed(string errorCode, DateTime nowUtc)
    {
        EnsureStatus(PendingActionStatus.Executing, nameof(MarkFailed));
        Status = PendingActionStatus.Failed;
        ErrorCode = errorCode;
        DecidedAt = nowUtc;
    }

    public void MarkStale(DateTime nowUtc)
    {
        EnsureStatus(PendingActionStatus.Executing, nameof(MarkStale));
        Status = PendingActionStatus.Stale;
        DecidedAt = nowUtc;
    }

    public void MarkRejected(DateTime nowUtc)
    {
        EnsureStatus(PendingActionStatus.Pending, nameof(MarkRejected));
        Status = PendingActionStatus.Rejected;
        DecidedAt = nowUtc;
    }

    private void EnsureStatus(PendingActionStatus expected, string transition)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Pending action {Id}: {transition} requires status {expected}, but it is {Status}.");
        }
    }
}
