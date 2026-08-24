using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Identity;
using AfterApply.Infrastructure.Applications;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddIdentityAndJwt(configuration);
        services.AddApplicationServices();
        services.AddValidatorsFromAssemblyContaining<CreateApplicationRequestValidator>();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Postgres when running via docker-compose.");

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Redis is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Redis \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Redis when running via docker-compose.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnectionString));

        services.AddHealthChecks()
            .AddNpgSql(postgresConnectionString, name: "postgres")
            .AddRedis(redisConnectionString, name: "redis");

        return services;
    }

    private static IServiceCollection AddIdentityAndJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. For local dev run " +
                "'dotnet user-secrets set Jwt:SigningKey \"<base64>\" --project src/AfterApply.Api', " +
                "or set Jwt__SigningKey when running via docker-compose.");

        var jwtOptions = new JwtOptions
        {
            SigningKey = signingKey,
            Issuer = configuration["Jwt:Issuer"] ?? "AfterApply",
            Audience = configuration["Jwt:Audience"] ?? "AfterApply.Api",
            AccessTokenMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 20),
            RefreshTokenDays = configuration.GetValue("Jwt:RefreshTokenDays", 30)
        };

        services.AddSingleton(Options.Create(jwtOptions));

        services.AddIdentityCore<ApplicationUser>(options => { options.User.RequireUniqueEmail = true; })
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICompanyResolver, CompanyResolver>();
        services.AddScoped<IApplicationService, ApplicationService>();

        return services;
    }
}
