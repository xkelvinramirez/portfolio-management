using Application.Common.Security;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Agent.Plugins;
using NSubstitute;

namespace WebApi.MinimalAPI.Tests.Agent;

public sealed class PortfolioAgentPluginTests
{
    [Fact]
    public async Task GetPortfolioAssetValueComparisonAsync_ReturnsSignedDifferencesPerAssetAcrossExchanges()
    {
        const long portfolioId = 0;
        var portfolio = Portfolio.Create(42, "Test", string.Empty);
        var portfolioRepository = Substitute.For<IPortfolioRepository>();
        portfolioRepository.GetByIdAsync(portfolioId, Arg.Any<CancellationToken>()).Returns(portfolio);

        var entryRepository = Substitute.For<IPortfolioEntryRepository>();
        entryRepository.GetByPortfolioIdAsync(portfolioId, Arg.Any<CancellationToken>()).Returns(
        [
            CreateEntry(portfolioId, 1, 1, "BTC", 2m, 100m, new DateTime(2026, 10, 1)),
            CreateEntry(portfolioId, 1, 2, "BTC", 1m, 50m, new DateTime(2026, 10, 1)),
            CreateEntry(portfolioId, 1, 1, "BTC", 1m, 120m, new DateTime(2026, 11, 1)),
            CreateEntry(portfolioId, 2, 1, "ETH", 1m, 200m, new DateTime(2026, 10, 1)),
            CreateEntry(portfolioId, 3, 1, "SOL", 5m, 10m, new DateTime(2026, 11, 1))
        ]);

        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(42);
        var plugin = new PortfolioAgentPlugin(portfolioRepository, entryRepository, currentUserProvider);

        var result = await plugin.GetPortfolioAssetValueComparisonAsync(
            portfolioId, "2026-10-06", "2026-11-05", TestContext.Current.CancellationToken);

        result.Should().NotBeNull().And.BeEquivalentTo(
        [
            new PortfolioAssetValueComparisonForAgent("BTC", 250m, 170m, -80m),
            new PortfolioAssetValueComparisonForAgent("ETH", 200m, 200m, 0m),
            new PortfolioAssetValueComparisonForAgent("SOL", 0m, 50m, 50m)
        ]);
    }

    [Fact]
    public async Task GetPortfolioAssetValueComparisonAsync_ReturnsNullWhenPortfolioIsNotOwned()
    {
        var portfolioRepository = Substitute.For<IPortfolioRepository>();
        portfolioRepository.GetByIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(Portfolio.Create(99, "Other", string.Empty));
        var entryRepository = Substitute.For<IPortfolioEntryRepository>();
        var currentUserProvider = Substitute.For<ICurrentUserProvider>();
        currentUserProvider.UserId.Returns(42);
        var plugin = new PortfolioAgentPlugin(portfolioRepository, entryRepository, currentUserProvider);

        var result = await plugin.GetPortfolioAssetValueComparisonAsync(
            1, "2026-10-06", "2026-11-05", TestContext.Current.CancellationToken);

        result.Should().BeNull();
        await entryRepository.DidNotReceive().GetByPortfolioIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("06/10/2026", "2026-11-05")]
    [InlineData("2026-11-05", "2026-10-06")]
    public async Task GetPortfolioAssetValueComparisonAsync_RejectsInvalidOrReversedDates(string from, string to)
    {
        var plugin = new PortfolioAgentPlugin(
            Substitute.For<IPortfolioRepository>(),
            Substitute.For<IPortfolioEntryRepository>(),
            Substitute.For<ICurrentUserProvider>());

        var act = () => plugin.GetPortfolioAssetValueComparisonAsync(
            1, from, to, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static PortfolioEntry CreateEntry(
        long portfolioId,
        long cryptoCurrencyId,
        long exchangeId,
        string symbol,
        decimal quantity,
        decimal pricePerUnit,
        DateTime recordedAt)
    {
        var entry = PortfolioEntry.Create(portfolioId, cryptoCurrencyId, exchangeId, quantity, pricePerUnit, recordedAt);
        entry.CryptoCurrency = CryptoCurrency.Create(symbol, symbol);
        return entry;
    }
}