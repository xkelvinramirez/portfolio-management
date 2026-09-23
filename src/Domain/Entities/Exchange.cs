using Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities;

public sealed class Exchange : Entity
{
    public string Name { get; set; } = string.Empty; // e.g., "Binance", "Coinbase"
    public string ApiKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}