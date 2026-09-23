using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Portfolios;

internal sealed class PortfolioEntryConfiguration : IEntityTypeConfiguration<PortfolioEntry>
{
    public void Configure(EntityTypeBuilder<PortfolioEntry> builder)
    {
        builder.ToTable("PortfolioEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Quantity).HasPrecision(28, 18);
        builder.Property(e => e.PricePerUnit).HasPrecision(28, 18);
        builder.Property(e => e.RecordedAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne<Portfolio>()
            .WithMany(p => p.Entries)
            .HasForeignKey(e => e.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        //builder.HasOne<CryptoCurrency>()
        builder.HasOne(e => e.CryptoCurrency)
            .WithMany()
            .HasForeignKey(e => e.CryptoCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        //builder.HasOne<Exchange>()
        builder.HasOne(e => e.Exchange)
            .WithMany()
            .HasForeignKey(e => e.ExchangeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
