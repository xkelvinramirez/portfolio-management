using WebApi.MinimalAPI.Endpoints.Portfolios;

namespace WebApi.MinimalAPI.Endpoints.Common;
public static class EndpointRegistrar
{
    public static WebApplication RegisterEndpoints(this WebApplication app)
    {
        app.RegisterPortfolioEndpoints();
        return app;
    }
}