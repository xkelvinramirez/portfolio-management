namespace Domain.Entities;

public sealed record PortfolioAssetValuationSeries(long CryptoCurrencyId, string Symbol, List<PortfolioValuationPoint> Points);
