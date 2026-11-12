using Application.Common.Behaviors;
using Application.MafAgent;
using Application.MafAgent.Interfaces;
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
                // CacheInvalidationBehavior wraps UnitOfWorkBehavior so invalidation runs after the commit.
                options.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));
                options.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
            });

        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // MafAgent (plan §0 "Registro y validadores"): scoped, like the repositories they consume.
        services.AddScoped<PendingActionFactory>();
        services.AddScoped<ICurrentValuesReader, CurrentValuesReader>();
        services.AddScoped<PendingActionExecutor>();

        return services;
    }
}