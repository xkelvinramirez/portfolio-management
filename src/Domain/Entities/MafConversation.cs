namespace Domain.Entities;

/// <summary>
/// A conversation with the Microsoft Agent Framework agent. Owned by exactly one user; persisted so it
/// survives API restarts (spec maf-tool-calling-agent AC15) and expires after 24 h without activity (AC16).
/// The agent session is stored encrypted at rest because it may contain decrypted note content.
/// </summary>
public sealed class MafConversation
{
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(24);

    private MafConversation()
    {
    }

    public Guid Id { get; private set; }
    public long UserId { get; private set; }
    public string EncryptedSessionState { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime LastActivityAt { get; private set; }

    public static MafConversation Create(long userId, string encryptedSessionState, DateTime nowUtc) =>
        new()
        {
            Id = Guid.CreateVersion7(nowUtc),
            UserId = userId,
            EncryptedSessionState = encryptedSessionState,
            CreatedAt = nowUtc,
            LastActivityAt = nowUtc,
        };

    /// <summary>Expired once strictly more than 24 h have passed since the last activity.</summary>
    public bool IsExpired(DateTime nowUtc) => nowUtc - LastActivityAt > InactivityTimeout;

    public void Touch(DateTime nowUtc) => LastActivityAt = nowUtc;

    public void UpdateSession(string encryptedSessionState, DateTime nowUtc)
    {
        EncryptedSessionState = encryptedSessionState;
        Touch(nowUtc);
    }
}
