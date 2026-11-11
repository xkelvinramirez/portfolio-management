using Domain.Entities;

namespace Application.MafAgent.Interfaces;

public interface IMafConversationRepository
{
    Task<MafConversation?> GetByIdAsync(Guid id, CancellationToken ct);

    void Add(MafConversation conversation);

    void Update(MafConversation conversation);
}
