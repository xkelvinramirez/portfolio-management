
namespace Contracts.Exchanges;

public sealed class UpdateExchangeRequest
{
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
