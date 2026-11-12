using Application.MafAgent;
using FluentAssertions;

namespace Application.Tests.MafAgent;

/// <summary>Plan T015: an edit is stale when any field of its Before snapshot no longer matches the current values (AC18).</summary>
public class StaleCheckTests
{
    private static Dictionary<string, string?> Snapshot() => new()
    {
        ["CryptoCurrencyId"] = "20",
        ["ExchangeId"] = "30",
        ["Quantity"] = "0.5",
        ["PricePerUnit"] = "60000",
        ["RecordedAt"] = "2026-11-10T18:30:00.0000000Z",
    };

    [Fact]
    public void EqualSnapshots_AreNotStale() =>
        StaleCheck.IsStale(Snapshot(), Snapshot()).Should().BeFalse();

    [Theory]
    [InlineData("CryptoCurrencyId", "21")]
    [InlineData("ExchangeId", "31")]
    [InlineData("Quantity", "0.6")]
    [InlineData("PricePerUnit", "60000.01")]
    [InlineData("RecordedAt", "2026-11-10T18:30:00.0000001Z")]
    public void AnyFieldThatChanged_IsStale(string field, string currentValue)
    {
        Dictionary<string, string?> current = Snapshot();
        current[field] = currentValue;

        StaleCheck.IsStale(Snapshot(), current).Should().BeTrue();
    }

    [Fact]
    public void AFieldThatBecameNull_IsStale()
    {
        Dictionary<string, string?> current = Snapshot();
        current["PricePerUnit"] = null;

        StaleCheck.IsStale(Snapshot(), current).Should().BeTrue();
    }

    [Fact]
    public void AFieldMissingFromCurrent_IsStale()
    {
        Dictionary<string, string?> current = Snapshot();
        current.Remove("Quantity");

        StaleCheck.IsStale(Snapshot(), current).Should().BeTrue();
    }
}
