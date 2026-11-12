using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MafAgent.Persistence;

internal sealed class MafConversationConfiguration : IEntityTypeConfiguration<MafConversation>
{
    public void Configure(EntityTypeBuilder<MafConversation> builder)
    {
        builder.ToTable("mafagent_conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.EncryptedSessionState).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.LastActivityAt).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.UserId);
    }
}
