using System.Text.Json;
using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Payloads;
using Contracts.MafAgent;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Application.Tests.MafAgent;

/// <summary>Plan T013: PendingActionFactory builds encrypted, typed pending actions and their user-facing DTO.</summary>
public class PendingActionFactoryTests
{
    private const long UserId = 7L;
    private const string EncryptedPrefix = "enc:";

    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime WinterUtc = new(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime SummerUtc = new(2026, 7, 10, 18, 30, 0, DateTimeKind.Utc);

    private readonly Guid _conversationId = Guid.NewGuid();
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly FakeTimeProvider _time = new(Now);

    public PendingActionFactoryTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(c => EncryptedPrefix + c.Arg<string>());
        _encryption.Decrypt(Arg.Any<string>()).Returns(c => c.Arg<string>()[EncryptedPrefix.Length..]);
        _currentUser.UserId.Returns(UserId);
    }

    private PendingActionFactory CreateFactory() => new(_encryption, _currentUser, _time);

    public static TheoryData<PendingActionOperation, object, Dictionary<string, string?>?, Dictionary<string, string?>> EveryOperation() => new()
    {
        {
            PendingActionOperation.CreatePortfolio,
            new CreatePortfolioPayload("Largo plazo", "Holdings a 5 años"),
            null,
            new() { ["Name"] = "Largo plazo", ["Description"] = "Holdings a 5 años" }
        },
        {
            PendingActionOperation.UpdatePortfolio,
            new UpdatePortfolioPayload(3, "Largo plazo", "Nueva descripción"),
            new() { ["Name"] = "Largo plazo", ["Description"] = "Vieja descripción" },
            new() { ["PortfolioId"] = "3", ["Name"] = "Largo plazo", ["Description"] = "Nueva descripción" }
        },
        {
            PendingActionOperation.CreateEntry,
            new CreateEntryPayload(3, 20, 30, 0.5m, 60000.10m, WinterUtc),
            null,
            new()
            {
                ["PortfolioId"] = "3", ["CryptoCurrencyId"] = "20", ["ExchangeId"] = "30",
                ["Quantity"] = "0.5", ["PricePerUnit"] = "60000.1", ["RecordedAt"] = "2026-11-10T18:30:00Z",
            }
        },
        {
            PendingActionOperation.UpdateEntry,
            new UpdateEntryPayload(9, 20, 30, 0.75m, 60000m, WinterUtc),
            new()
            {
                ["CryptoCurrencyId"] = "20", ["ExchangeId"] = "30", ["Quantity"] = "0.5",
                ["PricePerUnit"] = "60000", ["RecordedAt"] = "2026-11-10T18:30:00.0000000Z",
            },
            new()
            {
                ["EntryId"] = "9", ["CryptoCurrencyId"] = "20", ["ExchangeId"] = "30",
                ["Quantity"] = "0.75", ["PricePerUnit"] = "60000", ["RecordedAt"] = "2026-11-10T18:30:00Z",
            }
        },
        {
            PendingActionOperation.CreateNote,
            new CreateNotePayload("Ideas", "Rebalanceo", "Vender 10 % de ETH"),
            null,
            new() { ["Category"] = "Ideas", ["Title"] = "Rebalanceo", ["Content"] = "Vender 10 % de ETH" }
        },
        {
            PendingActionOperation.UpdateNote,
            new UpdateNotePayload("note-1", "Ideas", "Rebalanceo", "Vender 20 % de ETH", false),
            new() { ["Category"] = "Ideas", ["Title"] = "Rebalanceo", ["Content"] = "Vender 10 % de ETH", ["IsActive"] = "false" },
            new()
            {
                ["NoteId"] = "note-1", ["Category"] = "Ideas", ["Title"] = "Rebalanceo",
                ["Content"] = "Vender 20 % de ETH", ["IsActive"] = "false",
            }
        },
        {
            PendingActionOperation.RefreshPrices,
            new RefreshPricesPayload(3),
            null,
            new() { ["PortfolioId"] = "3" }
        },
        {
            PendingActionOperation.SendStatusReport,
            new SendStatusReportPayload(3, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)),
            null,
            new() { ["PortfolioId"] = "3", ["PeriodStart"] = "2026-10-01T00:00:00Z", ["PeriodEnd"] = "2026-10-31T00:00:00Z" }
        },
    };

    [Theory]
    [MemberData(nameof(EveryOperation))]
    public void Create_ForEveryOperation_ProducesAPendingActionWhoseDtoShowsTheExactValues(
        PendingActionOperation op, object payload, Dictionary<string, string?>? before, Dictionary<string, string?> expectedValues)
    {
        PendingActionFactory factory = CreateFactory();

        PendingAction action = factory.Create(_conversationId, op, payload, before);
        PendingActionDto dto = factory.ToDto(action);

        action.Operation.Should().Be(op);
        action.Status.Should().Be(PendingActionStatus.Pending);
        action.ConversationId.Should().Be(_conversationId);
        action.UserId.Should().Be(UserId);
        dto.Id.Should().Be(action.Id);
        dto.Operation.Should().Be(op.ToString());
        dto.Values.Should().BeEquivalentTo(expectedValues);
        factory.ReadPayload(action).Should().Be(payload);
    }

    [Theory]
    [MemberData(nameof(EveryOperation))]
    public void Create_EncryptsPayloadAndBefore_NothingIsStoredInClear(
        PendingActionOperation op, object payload, Dictionary<string, string?>? before, Dictionary<string, string?> expectedValues)
    {
        PendingAction action = CreateFactory().Create(_conversationId, op, payload, before);

        action.PayloadJson.Should().StartWith(EncryptedPrefix);
        _encryption.Received(before is null ? 1 : 2).Encrypt(Arg.Any<string>());
        using JsonDocument plainPayload = JsonDocument.Parse(action.PayloadJson[EncryptedPrefix.Length..]);
        plainPayload.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(expectedValues.Keys);
        if (before is null)
        {
            action.BeforeJson.Should().BeNull();
        }
        else
        {
            action.BeforeJson.Should().StartWith(EncryptedPrefix);
        }
    }

    [Fact]
    public void Create_EncryptsTheJsonOfThePayload()
    {
        CreateFactory().Create(_conversationId, PendingActionOperation.CreateNote, new CreateNotePayload("Ideas", "Rebalanceo", "texto secreto"), null);

        _encryption.Received(1).Encrypt(Arg.Is<string>(json => json.Contains("texto secreto")));
    }

    [Fact]
    public void Create_ExpiresTenMinutesAfterNow()
    {
        PendingActionFactory factory = CreateFactory();

        PendingAction action = factory.Create(_conversationId, PendingActionOperation.RefreshPrices, new RefreshPricesPayload(3), null);

        action.CreatedAt.Should().Be(Now.UtcDateTime);
        action.ExpiresAt.Should().Be(Now.UtcDateTime.AddMinutes(10));
        factory.ToDto(action).ExpiresAt.Should().Be("2026-11-11T10:10:00Z");
    }

    [Theory]
    [InlineData(PendingActionOperation.UpdatePortfolio)]
    [InlineData(PendingActionOperation.UpdateEntry)]
    [InlineData(PendingActionOperation.UpdateNote)]
    public void Create_EditWithBefore_KeepsTheFullSnapshotAsBefore(PendingActionOperation op)
    {
        (object payload, Dictionary<string, string?> before) = EditCase(op);
        PendingActionFactory factory = CreateFactory();

        PendingAction action = factory.Create(_conversationId, op, payload, before);

        factory.ReadBefore(action).Should().BeEquivalentTo(before);
    }

    [Fact]
    public void ToDto_EditBefore_ShowsDatesInPresentationFormat()
    {
        (object payload, Dictionary<string, string?> before) = EditCase(PendingActionOperation.UpdateEntry);
        PendingActionFactory factory = CreateFactory();

        PendingActionDto dto = factory.ToDto(factory.Create(_conversationId, PendingActionOperation.UpdateEntry, payload, before));

        dto.Before.Should().NotBeNull();
        dto.Before!["RecordedAt"].Should().Be("2026-11-10T18:30:00Z");
        dto.Before["Quantity"].Should().Be("0.5");
    }

    [Theory]
    [InlineData(PendingActionOperation.UpdatePortfolio)]
    [InlineData(PendingActionOperation.UpdateEntry)]
    [InlineData(PendingActionOperation.UpdateNote)]
    public void Create_EditWhoseBeforeMissesAFieldOfTheFullRequest_Throws(PendingActionOperation op)
    {
        (object payload, Dictionary<string, string?> before) = EditCase(op);
        before.Remove(before.Keys.First());

        Action create = () => CreateFactory().Create(_conversationId, op, payload, before);

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(PendingActionOperation.UpdatePortfolio)]
    [InlineData(PendingActionOperation.UpdateEntry)]
    [InlineData(PendingActionOperation.UpdateNote)]
    public void Create_EditWithoutBefore_Throws(PendingActionOperation op)
    {
        (object payload, _) = EditCase(op);

        Action create = () => CreateFactory().Create(_conversationId, op, payload, null);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NonEditWithBefore_Throws()
    {
        Action create = () => CreateFactory().Create(
            _conversationId, PendingActionOperation.RefreshPrices, new RefreshPricesPayload(3), new Dictionary<string, string?>());

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_PayloadOfAnotherOperation_Throws()
    {
        Action create = () => CreateFactory().Create(
            _conversationId, PendingActionOperation.CreatePortfolio, new RefreshPricesPayload(3), null);

        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_DateThatIsNotUtc_Throws(DateTimeKind kind)
    {
        var payload = new CreateEntryPayload(3, 20, 30, 1m, 1m, DateTime.SpecifyKind(WinterUtc, kind));

        Action create = () => CreateFactory().Create(_conversationId, PendingActionOperation.CreateEntry, payload, null);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReadPayload_ReturnsDatesAsUtc()
    {
        PendingActionFactory factory = CreateFactory();
        PendingAction action = factory.Create(
            _conversationId, PendingActionOperation.CreateEntry, new CreateEntryPayload(3, 20, 30, 1m, 1m, WinterUtc), null);

        var payload = (CreateEntryPayload)factory.ReadPayload(action);

        payload.RecordedAt.Kind.Should().Be(DateTimeKind.Utc);
        payload.RecordedAt.Should().Be(WinterUtc);
    }

    [Fact]
    public void Summary_InWinter_ShowsMadridLocalTimeWithPlusOneOffset()
    {
        PendingActionFactory factory = CreateFactory();

        PendingActionDto dto = factory.ToDto(factory.Create(
            _conversationId, PendingActionOperation.CreateEntry, new CreateEntryPayload(3, 20, 30, 1m, 1m, WinterUtc), null));

        dto.Summary.Should().Contain("2026-11-10T19:30:00+01:00 (Europe/Madrid)");
    }

    [Fact]
    public void Summary_InSummer_ShowsMadridLocalTimeWithPlusTwoOffset()
    {
        PendingActionFactory factory = CreateFactory();

        PendingActionDto dto = factory.ToDto(factory.Create(
            _conversationId, PendingActionOperation.CreateEntry, new CreateEntryPayload(3, 20, 30, 1m, 1m, SummerUtc), null));

        dto.Summary.Should().Contain("2026-07-10T20:30:00+02:00 (Europe/Madrid)");
    }

    [Fact]
    public void Summary_OfANoteEdit_NamesTheChangedFieldsWithoutRepeatingFreeText()
    {
        var payload = new UpdateNotePayload("note-1", "Ideas", "Título nuevo secreto", "Contenido nuevo secreto", true);
        var before = new Dictionary<string, string?>
        {
            ["Category"] = "Ideas",
            ["Title"] = "Título viejo secreto",
            ["Content"] = "Contenido viejo secreto",
            ["IsActive"] = "true",
        };
        PendingActionFactory factory = CreateFactory();

        PendingActionDto dto = factory.ToDto(factory.Create(_conversationId, PendingActionOperation.UpdateNote, payload, before));

        dto.Summary.Should().Contain("note-1").And.Contain("Title").And.Contain("Content");
        dto.Summary.Should().NotContain("Category").And.NotContain("IsActive");
        dto.Summary.Should().NotContain("secreto");
    }

    [Fact]
    public void Summary_OfCreates_DoesNotRepeatFreeText()
    {
        PendingActionFactory factory = CreateFactory();

        string notes = factory.ToDto(factory.Create(
            _conversationId, PendingActionOperation.CreateNote, new CreateNotePayload("Cat secreta", "Título secreto", "Contenido secreto"), null)).Summary;
        string portfolio = factory.ToDto(factory.Create(
            _conversationId, PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Nombre secreto", "Descripción secreta"), null)).Summary;

        notes.Should().NotContain("secret");
        portfolio.Should().NotContain("secret");
    }

    private static (object Payload, Dictionary<string, string?> Before) EditCase(PendingActionOperation op) => op switch
    {
        PendingActionOperation.UpdatePortfolio => (
            new UpdatePortfolioPayload(3, "Largo plazo", "Nueva"),
            new Dictionary<string, string?> { ["Name"] = "Largo plazo", ["Description"] = "Vieja" }),
        PendingActionOperation.UpdateEntry => (
            new UpdateEntryPayload(9, 20, 30, 0.75m, 60000m, WinterUtc),
            new Dictionary<string, string?>
            {
                ["CryptoCurrencyId"] = "20",
                ["ExchangeId"] = "30",
                ["Quantity"] = "0.5",
                ["PricePerUnit"] = "60000",
                ["RecordedAt"] = "2026-11-10T18:30:00.0000000Z",
            }),
        PendingActionOperation.UpdateNote => (
            new UpdateNotePayload("note-1", "Ideas", "Rebalanceo", "Nuevo", false),
            new Dictionary<string, string?> { ["Category"] = "Ideas", ["Title"] = "Rebalanceo", ["Content"] = "Viejo", ["IsActive"] = "false" }),
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}
