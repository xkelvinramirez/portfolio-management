using Application.MafAgent;
using FluentAssertions;

namespace Application.Tests.MafAgent;

/// <summary>
/// Review of T013–T016: the snapshot of a date must not depend on the DateTime Kind a provider returns,
/// or an unchanged resource would look stale (AC18).
/// </summary>
public class FieldSnapshotTests
{
    private static readonly DateTime Utc = new(2026, 11, 10, 18, 30, 0, 123, DateTimeKind.Utc);
    private const string Expected = "2026-11-10T18:30:00.1230000Z";

    [Fact]
    public void UtcDate_IsRoundTripUtc() =>
        FieldSnapshot.Format(Utc).Should().Be(Expected);

    [Fact]
    public void UnspecifiedDate_IsTakenAsUtc_NotShiftedByTheMachineZone() =>
        FieldSnapshot.Format(DateTime.SpecifyKind(Utc, DateTimeKind.Unspecified)).Should().Be(Expected);

    [Fact]
    public void LocalDate_IsConvertedToTheSameUtcInstant() =>
        FieldSnapshot.Format(Utc.ToLocalTime()).Should().Be(Expected);

    [Fact]
    public void DateTimeOffset_IsTheSameUtcInstant() =>
        FieldSnapshot.Format(new DateTimeOffset(Utc).ToOffset(TimeSpan.FromHours(1))).Should().Be(Expected);

    [Theory]
    [InlineData("1.50000000", "1.5")]
    [InlineData("60000.100", "60000.1")]
    [InlineData("60000", "60000")]
    public void Decimals_DropTrailingZeros_SoTheScaleNeverLooksLikeAChange(string value, string expected) =>
        FieldSnapshot.Format(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);
}
