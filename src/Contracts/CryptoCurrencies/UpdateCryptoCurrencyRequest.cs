
namespace Contracts.CryptoCurrencies;

public sealed class UpdateCryptoCurrencyRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
