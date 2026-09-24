using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities;

public sealed class Portfolio : Entity
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Property navigation for related PortfolioEntry entities
    public List<PortfolioEntry> Entries { get; set; } = new List<PortfolioEntry>();


    public Portfolio() { }
    public static Portfolio Create(long userId, string name, string description)
    {
        return new Portfolio
        {
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }
    public decimal GetTotalValue(List<PortfolioEntry> entries)
    {
        return entries
            .Where(e => e.PortfolioId == Id)
            .Sum(e => e.Quantity * e.PricePerUnit);
    }

    public decimal GetPortfolioValueByDate(List<PortfolioEntry> entries, DateTime date)
    {
        return GetHoldingsAsOf(entries, date).Sum(e => e.Quantity * e.PricePerUnit);
    }

    public List<PortfolioEntry> GetCryptoCurrencyHistory(List<PortfolioEntry> entries, long cryptoCurrencyId)
    {
        return entries
            .Where(e => e.PortfolioId == Id && e.CryptoCurrencyId == cryptoCurrencyId)
            .OrderByDescending(e => e.RecordedAt)
            .ToList();
    }

    /// <summary>
    /// The current snapshot as of a date: one entry per (CryptoCurrency, Exchange) pair,
    /// the latest recorded one on or before the date. Backs both the holdings table and allocation views.
    /// </summary>
    public List<PortfolioEntry> GetHoldingsAsOf(List<PortfolioEntry> entries, DateTime date)
    {
        return entries
            .Where(e => e.PortfolioId == Id && e.RecordedAt.Date <= date.Date)
            .GroupBy(e => new { e.CryptoCurrencyId, e.ExchangeId })
            .Select(g => g.OrderByDescending(e => e.RecordedAt).First())
            .ToList();
    }

    public List<PortfolioValuationPoint> GetValueHistory(List<PortfolioEntry> entries)
    {
        var relevant = entries.Where(e => e.PortfolioId == Id).ToList();

        return relevant
            .Select(e => e.RecordedAt.Date)
            .Distinct()
            .OrderBy(date => date)
            .Select(date => new PortfolioValuationPoint(date, GetPortfolioValueByDate(relevant, date)))
            .ToList();
    }
}
