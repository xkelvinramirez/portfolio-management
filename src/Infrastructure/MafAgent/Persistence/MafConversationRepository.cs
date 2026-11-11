using Application.MafAgent.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.MafAgent.Persistence;

internal sealed class MafConversationRepository(AppDbContext dbContext) : IMafConversationRepository
{
    public Task<MafConversation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.MafConversations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(MafConversation conversation) => dbContext.MafConversations.Add(conversation);

    public void Update(MafConversation conversation) => dbContext.MafConversations.Update(conversation);
}
