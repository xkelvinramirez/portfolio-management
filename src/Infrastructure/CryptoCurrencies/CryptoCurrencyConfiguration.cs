using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.CryptoCurrencies;

internal sealed class CryptoCurrencyConfiguration : IEntityTypeConfiguration<CryptoCurrency>
{
    public void Configure(EntityTypeBuilder<CryptoCurrency> builder)
    {
        builder.ToTable("CryptoCurrencies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Symbol).IsRequired().HasMaxLength(20);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => c.Symbol).IsUnique();
    }
}
