using Application.Common.Behaviors;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services,
        ConfigurationManager configManager)
    {
        services.AddServices()
            .AddMediatR(options =>
            {
                if (configManager["MEDIATR_LICENSE_KEY"] is not null)
                {
                    options.LicenseKey = configManager["MEDIATR_LICENSE_KEY"];
                }
                options.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
                options.AddOpenBehavior(typeof(ValidationBehavior<,>));
                options.AddOpenBehavior(typeof(LoggingBehavior<,>));
            });

        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services;
    }
}