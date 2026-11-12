using Application.Common.UnitOfWork;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Common.Persistence.Contexts;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<PortfolioEntry> PortfolioEntries => Set<PortfolioEntry>();
    public DbSet<CryptoCurrency> CryptoCurrencies => Set<CryptoCurrency>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<MafConversation> MafConversations => Set<MafConversation>();
    public DbSet<PendingAction> PendingActions => Set<PendingAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}