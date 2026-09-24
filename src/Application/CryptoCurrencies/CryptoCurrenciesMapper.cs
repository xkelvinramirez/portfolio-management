using Contracts.CryptoCurrencies;
using Domain.Entities;

namespace Application.CryptoCurrencies;

public static class CryptoCurrenciesMapper
{
    public static CreateCryptoCurrencyResponse ToCreateCryptoCurrencyResponse(this CryptoCurrency cryptoCurrency)
        => new(cryptoCurrency.Id, cryptoCurrency.Symbol, cryptoCurrency.Name, cryptoCurrency.CreatedAt);

    public static UpdateCryptoCurrencyResponse ToUpdateCryptoCurrencyResponse(this CryptoCurrency cryptoCurrency)
        => new(cryptoCurrency.Id, cryptoCurrency.Symbol, cryptoCurrency.Name, cryptoCurrency.CreatedAt);

    public static CryptoCurrencyResponse ToCryptoCurrencyResponse(this CryptoCurrency cryptoCurrency)
        => new(cryptoCurrency.Id, cryptoCurrency.Symbol, cryptoCurrency.Name, cryptoCurrency.CreatedAt);
}
