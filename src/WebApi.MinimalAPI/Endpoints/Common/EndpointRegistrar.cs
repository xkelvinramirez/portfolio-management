namespace WebApi.MinimalAPI.Endpoints.Common;
public static class EndpointRegistrar
{
    public static WebApplication RegisterEndpoints(this WebApplication app)
    {
        //app.RegisterAgentEndpoints();
        //app.RegisterBacklogItemEndpoints();
        //app.RegisterCategoryEndpoints();
        return app;
    }
}