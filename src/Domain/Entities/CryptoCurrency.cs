using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities;

public sealed class CryptoCurrency : Entity
{
    public string Symbol { get; set; } = string.Empty; // e.g., "BTC", "ETH"
    public string Name { get; set; } = string.Empty; // e.g., "Bitcoin", "Ethereum"
    public DateTime CreatedAt { get; set; }
}
