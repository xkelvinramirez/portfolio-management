using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities;

public sealed class PortfolioEntry : Entity
{
    public long PortfolioId { get; set; }
    public long CryptoCurrencyId { get; set; }
    public long ExchangeId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PricePerUnit { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public CryptoCurrency CryptoCurrency { get; set; } = null!;
    public Exchange Exchange { get; set; } = null!;
}