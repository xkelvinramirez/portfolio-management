using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MafAgent.Persistence;

internal sealed class PendingActionConfiguration : IEntityTypeConfiguration<PendingAction>
{
    public void Configure(EntityTypeBuilder<PendingAction> builder)
    {
        builder.ToTable("mafagent_pending_actions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        // Stored as text so the audit trail stays readable in SQL and an enum reorder can't remap rows.
        builder.Property(a => a.Operation).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Payload, before-snapshot and result arrive already encrypted by the application layer.
        builder.Property(a => a.PayloadJson).IsRequired();
        builder.Property(a => a.BeforeJson);
        builder.Property(a => a.ResultJson);
        builder.Property(a => a.ErrorCode).HasMaxLength(128);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.ExpiresAt).IsRequired();

        builder.HasOne<MafConversation>()
            .WithMany()
            .HasForeignKey(a => a.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ConversationId);
        builder.HasIndex(a => a.UserId);
    }
}
