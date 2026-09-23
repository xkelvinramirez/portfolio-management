using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Exchanges;

internal sealed class ExchangeConfiguration : IEntityTypeConfiguration<Exchange>
{
    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        builder.ToTable("Exchanges");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
        builder.Property(e => e.ApiKey).IsRequired().HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.Name).IsUnique();
    }
}
