
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Portfolios;

internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.UserId);
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(p => p.CreatedAt)
            .IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);
        //builder.HasMany(p => p.Entries)
        //    .WithOne()
        //    .HasForeignKey(e => e.PortfolioId)
        //    .OnDelete(DeleteBehavior.Cascade);
    }
}
