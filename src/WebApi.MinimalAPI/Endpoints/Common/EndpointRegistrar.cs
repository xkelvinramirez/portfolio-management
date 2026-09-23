using WebApi.MinimalAPI.Endpoints.Portfolios;
using WebApi.MinimalAPI.Endpoints.Users;

namespace WebApi.MinimalAPI.Endpoints.Common;
public static class EndpointRegistrar
{
    public static WebApplication RegisterEndpoints(this WebApplication app)
    {
        app.RegisterUserEndpoints();
        app.RegisterPortfolioEndpoints();
        return app;
    }
}