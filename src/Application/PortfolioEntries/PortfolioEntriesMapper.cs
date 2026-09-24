using Contracts.PortfolioEntries;
using Domain.Entities;

namespace Application.PortfolioEntries;

public static class PortfolioEntriesMapper
{
    public static CreatePortfolioEntryResponse ToCreatePortfolioEntryResponse(this PortfolioEntry entry)
        => new(entry.Id, entry.PortfolioId, entry.CryptoCurrencyId, entry.ExchangeId, entry.Quantity, entry.PricePerUnit, entry.RecordedAt);

    public static UpdatePortfolioEntryResponse ToUpdatePortfolioEntryResponse(this PortfolioEntry entry)
        => new(entry.Id, entry.PortfolioId, entry.CryptoCurrencyId, entry.ExchangeId, entry.Quantity, entry.PricePerUnit, entry.RecordedAt);

    public static PortfolioEntryResponse ToPortfolioEntryResponse(this PortfolioEntry entry)
        => new(entry.Id, entry.PortfolioId, entry.CryptoCurrencyId, entry.ExchangeId, entry.Quantity, entry.PricePerUnit, entry.RecordedAt);
}
