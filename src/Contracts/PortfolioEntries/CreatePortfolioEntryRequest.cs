
namespace Contracts.PortfolioEntries;

public sealed class CreatePortfolioEntryRequest
{
    public long PortfolioId { get; set; }
    public long CryptoCurrencyId { get; set; }
    public long ExchangeId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public DateTime RecordedAt { get; set; }
}
