using System.Linq.Expressions;
using Application.Common.Security;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.Common;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Agent;
using Infrastructure.Agent.Filters;
using Infrastructure.Agent.Plugins;
using Infrastructure.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;

namespace WebApi.MinimalAPI.Tests.Agent;

/// <summary>
/// Characterization baseline for the Semantic Kernel agent (spec maf-tool-calling-agent, AC11; plan T039).
/// These tests pin today's behaviour on `development` BEFORE any Microsoft Agent Framework package is added,
/// so a transitive dependency bump or shared-wiring change that alters the SK agent turns them red.
/// They never touch the network: connectors are only constructed, never called.
/// </summary>
public sealed class SkAgentRegressionTests
{
    private const long CurrentUserId = 7;
    private const long OtherUserId = 8;
    private const long PortfolioId = 1;

    private static readonly string[] ExpectedFunctionNames =
    [
        "ListMyPortfolios",
        "GetPortfolioValue",
        "GetPortfolioHoldings",
        "GetAssetPeakOrTroughDate",
        // Added on development by PR #19 (feature/portfolio-asset-value-comparison). Its data behaviour
        // is pinned by PortfolioAgentPluginTests; here we only pin that the kernel registers it.
        "GetPortfolioAssetValueComparison",
    ];

    private readonly IPortfolioRepository _portfolioRepository = Substitute.For<IPortfolioRepository>();
    private readonly IPortfolioEntryRepository _portfolioEntryRepository = Substitute.For<IPortfolioEntryRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();

    public SkAgentRegressionTests()
    {
        _currentUserProvider.UserId.Returns(CurrentUserId);
    }

    // ---------- Kernel construction (KernelFactory) ----------

    [Fact]
    public void CreateKernel_RegistersPortfolioOperationsPluginWithTheExpectedReadFunctions()
    {
        Kernel kernel = CreateKernelFactory().CreateKernel();

        kernel.Plugins.Should().ContainSingle(p => p.Name == "PortfolioOperations");
        KernelPlugin plugin = kernel.Plugins["PortfolioOperations"];
        plugin.Select(f => f.Name).Should().BeEquivalentTo(ExpectedFunctionNames);
    }

    [Fact]
    public void CreateKernel_RegistersLoopCappingAndToolAuditFilters()
    {
        Kernel kernel = CreateKernelFactory().CreateKernel();

        kernel.AutoFunctionInvocationFilters.Should().ContainSingle(f => f is LoopCappingFilter);
        kernel.FunctionInvocationFilters.Should().ContainSingle(f => f is ToolInvocationAuditFilter);
    }

    [Fact]
    public void CreateKernel_ResolvesFastAndReasoningChatServicesWithoutNetwork()
    {
        Kernel kernel = CreateKernelFactory().CreateKernel();

        kernel.GetRequiredService<IChatCompletionService>(KernelFactory.ReasoningModelServiceId).Should().NotBeNull();
        kernel.GetRequiredService<IChatCompletionService>(KernelFactory.FastUtilityModelServiceId).Should().NotBeNull();
    }

    // ---------- Plugin data (PortfolioAgentPlugin) ----------

    [Fact]
    public async Task ListMyPortfolios_ReturnsOnlyCurrentUserPortfoliosMappedToSummaries()
    {
        Expression<Func<Portfolio, bool>>? capturedPredicate = null;
        _portfolioRepository
            .GetAllAsync(1, 50, Arg.Do<Expression<Func<Portfolio, bool>>>(p => capturedPredicate = p), Arg.Any<CancellationToken>())
            .Returns(new PaginatorResponse<Portfolio>
            {
                Data = [Portfolio.Create(CurrentUserId, "Principal", "Largo plazo")],
            });

        IReadOnlyList<PortfolioSummaryForAgent> result = await CreatePlugin().ListMyPortfoliosAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().Be(new PortfolioSummaryForAgent(0, "Principal", "Largo plazo"));
        capturedPredicate.Should().NotBeNull();
        Func<Portfolio, bool> predicate = capturedPredicate!.Compile();
        predicate(new Portfolio { UserId = CurrentUserId }).Should().BeTrue();
        predicate(new Portfolio { UserId = OtherUserId }).Should().BeFalse();
    }

    [Fact]
    public async Task GetPortfolioValue_OwnedPortfolio_ReturnsSumOfQuantityTimesPrice()
    {
        SeedOwnedPortfolioWithEntries();

        decimal? value = await CreatePlugin().GetPortfolioValueAsync(PortfolioId, TestContext.Current.CancellationToken);

        // BTC 2 × 100 + ETH 3 × 10 (GetTotalValue sums every entry of the portfolio).
        value.Should().Be(230m);
    }

    [Fact]
    public async Task GetPortfolioValue_PortfolioOfAnotherUser_ReturnsNull()
    {
        _portfolioRepository.GetByIdAsync(PortfolioId, Arg.Any<CancellationToken>())
            .Returns(new Portfolio { UserId = OtherUserId });

        decimal? value = await CreatePlugin().GetPortfolioValueAsync(PortfolioId, TestContext.Current.CancellationToken);

        value.Should().BeNull();
        await _portfolioEntryRepository.DidNotReceive().GetByPortfolioIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPortfolioHoldings_OwnedPortfolio_ReturnsHoldingsOrderedByValueDescending()
    {
        SeedOwnedPortfolioWithEntries();

        IReadOnlyList<PortfolioHoldingForAgent>? holdings = await CreatePlugin().GetPortfolioHoldingsAsync(PortfolioId, TestContext.Current.CancellationToken);

        holdings.Should().Equal(
            new PortfolioHoldingForAgent("BTC", 2m, 200m),
            new PortfolioHoldingForAgent("ETH", 3m, 30m));
    }

    [Fact]
    public async Task GetAssetPeakOrTroughDate_Max_ReturnsDateOfHighestValue()
    {
        SeedOwnedPortfolioWithBtcHistory();

        AssetValuePointForAgent? point = await CreatePlugin().GetAssetPeakOrTroughDateAsync(PortfolioId, "btc", "max", cancellationToken: TestContext.Current.CancellationToken);

        point.Should().Be(new AssetValuePointForAgent("BTC", new DateTime(2026, 2, 1), 300m));
    }

    [Fact]
    public async Task GetAssetPeakOrTroughDate_Min_ReturnsDateOfLowestValue()
    {
        SeedOwnedPortfolioWithBtcHistory();

        AssetValuePointForAgent? point = await CreatePlugin().GetAssetPeakOrTroughDateAsync(PortfolioId, "BTC", "min", cancellationToken: TestContext.Current.CancellationToken);

        point.Should().Be(new AssetValuePointForAgent("BTC", new DateTime(2026, 3, 1), 50m));
    }

    [Fact]
    public async Task GetAssetPeakOrTroughDate_WithDateRange_OnlyConsidersPointsInsideTheRange()
    {
        SeedOwnedPortfolioWithBtcHistory();

        AssetValuePointForAgent? point = await CreatePlugin()
            .GetAssetPeakOrTroughDateAsync(PortfolioId, "BTC", "max", from: "2026-02-15", to: "2026-03-31", cancellationToken: TestContext.Current.CancellationToken);

        point.Should().Be(new AssetValuePointForAgent("BTC", new DateTime(2026, 3, 1), 50m));
    }

    [Fact]
    public async Task GetAssetPeakOrTroughDate_UnknownSymbol_ReturnsNull()
    {
        SeedOwnedPortfolioWithBtcHistory();

        AssetValuePointForAgent? point = await CreatePlugin().GetAssetPeakOrTroughDateAsync(PortfolioId, "DOGE", "max", cancellationToken: TestContext.Current.CancellationToken);

        point.Should().BeNull();
    }

    // ---------- Helpers ----------

    private PortfolioAgentPlugin CreatePlugin() =>
        new(_portfolioRepository, _portfolioEntryRepository, _currentUserProvider);

    private KernelFactory CreateKernelFactory()
    {
        var options = new AiOptions
        {
            Provider = AiProviderType.Ollama,
            FastModel = new ModelOptions { ModelId = "qwen2.5:3b-instruct", Endpoint = "http://localhost:11434/v1" },
            ReasoningModel = new ModelOptions { ModelId = "qwen2.5:3b-instruct", Endpoint = "http://localhost:11434/v1" },
            MaxAllowedToolLoops = 5,
        };

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(_portfolioRepository);
        services.AddSingleton(_portfolioEntryRepository);
        services.AddSingleton(_currentUserProvider);
        services.AddSingleton(sp => new LoopCappingFilter(
            sp.GetRequiredService<ILogger<LoopCappingFilter>>(), options.MaxAllowedToolLoops));
        services.AddSingleton<ToolInvocationAuditFilter>();
        services.AddSingleton<PortfolioAgentPlugin>();
        ServiceProvider provider = services.BuildServiceProvider();

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("InferenceClient").Returns(_ => new HttpClient());

        return new KernelFactory(provider, Options.Create(options), httpClientFactory, NullLoggerFactory.Instance);
    }

    // Portfolios built with `new Portfolio()` keep Id = 0, so their entries use PortfolioId 0
    // (the domain methods match entries by PortfolioId == portfolio.Id).
    private void SeedOwnedPortfolioWithEntries()
    {
        _portfolioRepository.GetByIdAsync(PortfolioId, Arg.Any<CancellationToken>())
            .Returns(new Portfolio { UserId = CurrentUserId });
        _portfolioEntryRepository.GetByPortfolioIdAsync(PortfolioId, Arg.Any<CancellationToken>())
            .Returns(
            [
                // Lower-value asset first on purpose: holdings come out in insertion order unless the
                // plugin sorts them, so this order is what makes the "descending by value" test discriminate.
                Entry(cryptoCurrencyId: 2, "ETH", quantity: 3m, pricePerUnit: 10m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                Entry(cryptoCurrencyId: 1, "BTC", quantity: 2m, pricePerUnit: 100m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            ]);
    }

    private void SeedOwnedPortfolioWithBtcHistory()
    {
        _portfolioRepository.GetByIdAsync(PortfolioId, Arg.Any<CancellationToken>())
            .Returns(new Portfolio { UserId = CurrentUserId });
        _portfolioEntryRepository.GetByPortfolioIdAsync(PortfolioId, Arg.Any<CancellationToken>())
            .Returns(
            [
                Entry(cryptoCurrencyId: 1, "BTC", quantity: 1m, pricePerUnit: 100m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                Entry(cryptoCurrencyId: 1, "BTC", quantity: 1m, pricePerUnit: 300m, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
                Entry(cryptoCurrencyId: 1, "BTC", quantity: 1m, pricePerUnit: 50m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            ]);
    }

    private static PortfolioEntry Entry(long cryptoCurrencyId, string symbol, decimal quantity, decimal pricePerUnit, DateTime recordedAt)
    {
        PortfolioEntry entry = PortfolioEntry.Create(0, cryptoCurrencyId, exchangeId: 1, quantity, pricePerUnit, recordedAt);
        entry.CryptoCurrency = CryptoCurrency.Create(symbol, symbol);
        return entry;
    }
}
