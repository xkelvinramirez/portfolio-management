
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Portfolios;

internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(p => p.Id);
        // A composite unique index (leading with UserId) both enforces "unique per user,
        // not globally" at the database level and still serves lookups filtered by UserId
        // alone, so it replaces the old standalone, non-unique UserId index.
        builder.HasIndex(p => new { p.UserId, p.Name }).IsUnique();
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(p => p.CreatedAt)
            .IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
    }
}
