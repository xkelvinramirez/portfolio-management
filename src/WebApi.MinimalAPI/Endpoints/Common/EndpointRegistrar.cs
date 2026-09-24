using WebApi.MinimalAPI.Endpoints.Portfolios;
using WebApi.MinimalAPI.Endpoints.Users;
using WebApi.MinimalAPI.Endpoints.Exchanges;
using WebApi.MinimalAPI.Endpoints.CryptoCurrencies;
using WebApi.MinimalAPI.Endpoints.PortfolioEntries;

namespace WebApi.MinimalAPI.Endpoints.Common;
public static class EndpointRegistrar
{
    public static WebApplication RegisterEndpoints(this WebApplication app)
    {
        app.RegisterUserEndpoints();
        app.RegisterPortfolioEndpoints();
        app.RegisterExchangeEndpoints();
        app.RegisterCryptoCurrencyEndpoints();
        app.RegisterPortfolioEntryEndpoints();
        return app;
    }
}