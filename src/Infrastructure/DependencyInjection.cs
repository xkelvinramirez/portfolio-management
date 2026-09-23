
using Application.Common.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using Infrastructure.Common.Options;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Security;
using Infrastructure.Portfolios;
using Infrastructure.Users;
using Application.Common.Security;
using Application.Portfolios.Interfaces;
using Application.Users.Interfaces;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        ConfigurationManager configuration, IHostEnvironment environment)
    {
        services
            .AddConfigurationOptions(configuration)
            .AddLoggingConfiguration(configuration)
            .AddBackgroundServices(configuration)
            .AddDbContexts(configuration)
            .AddRepositories()
            .AddPackages(configuration)
            .AddAdapters()
            .AddSecurity()
            .AddHealthChecksForDependencies(configuration);

        return services;
    }

    private static IServiceCollection AddBackgroundServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }

    private static IServiceCollection AddDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
       // services.AddDbContextPool<AppDbContext>(options =>
       //    options.UseInMemoryDatabase("AgentsPocDb")
       //);
        string? connectionString = configuration.GetConnectionString("Database");
        services.AddDbContext<AppDbContext>(
            options => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }

    private static IServiceCollection AddLoggingConfiguration(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSerilog(loggerConfiguration => loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .WriteTo.Conditional(static _ => Debugger.IsAttached, static writeTo => writeTo.Console()));

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        //services.AddScoped<IBacklogItemRepository, BacklogItemRepository>();
        //services.AddScoped<ICategoryRepository, CategoryRepository>();
        return services;
    }

    private static IServiceCollection AddAdapters(this IServiceCollection services)
    {
        return services;
    }

    private static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        return services;
    }



    private static IServiceCollection AddPackages(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services;
    }

    private static IServiceCollection AddConfigurationOptions(this IServiceCollection services,
        ConfigurationManager configuration)
    {
        services
            .AddOptions<ConnectionStringOptions>()
            .Bind(configuration.GetRequiredSection(ConnectionStringOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetRequiredSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    private static void AddHealthChecksForDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!, name: "postgres");

        services.AddHealthChecksUI(setup =>
        {
            setup.AddHealthCheckEndpoint("General", "/health/json");
            setup.SetHeaderText(Assembly.GetEntryAssembly()?.GetName().Name ?? "Healthcheck");
        }
        ).AddInMemoryStorage();
    }
}