
using Asp.Versioning;
using Microsoft.AspNetCore.ResponseCompression;
using WebApi.MinimalAPI.Endpoints.Common;

namespace WebApi.MinimalAPI;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        //services.AddAuthorization();

        services.AddProblemDetails();

        services.AddExceptionHandler<GlobalExceptionHandler>();

        //services.AddOutputCachingConfiguration(configuration);

        services.AddResponseCompression(options =>
        {
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["text/plain"]);
        });

        services.AddEndpointsApiExplorer();
#pragma warning disable IL2026 // AddApiExplorer usa reflexión marcada RequiresUnreferencedCode; el registro de servicios en el arranque de esta Minimal API (sin controladores MVC reales) funciona correctamente bajo AOT/trimming en este escenario.
        services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader())
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi();
#pragma warning restore IL2026

        return services;
    }

}
