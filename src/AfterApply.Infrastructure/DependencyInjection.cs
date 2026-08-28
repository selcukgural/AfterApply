using AfterApply.Application.Analytics;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Companies;
using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.Identity;
using AfterApply.Application.Imports;
using AfterApply.Application.Matching;
using AfterApply.Application.Metrics;
using AfterApply.Application.Notifications;
using AfterApply.Infrastructure.Analytics;
using AfterApply.Infrastructure.Applications;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.CompanyIntelligence;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Imports;
using AfterApply.Infrastructure.Matching;
using AfterApply.Infrastructure.Metrics;
using AfterApply.Infrastructure.Notifications;
using AfterApply.Infrastructure.Persistence;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure;

public static class DependencyInjection
{
    public const string CorsPolicyName = "Frontend";
    public const string AuthRateLimitPolicy = "auth-strict";
    public const string UploadRateLimitPolicy = "upload";
    public const string MatchingRateLimitPolicy = "matching";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLocalization();
        services.AddPersistence(configuration);
        services.AddIdentityAndJwt(configuration);
        services.AddApplicationServices();
        services.AddBackgroundJobs(configuration);
        services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();
        services.Configure<ImportOptions>(configuration.GetSection("Imports"));
        services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
        services.Configure<GoogleOAuthOptions>(configuration.GetSection("GoogleOAuth"));
        services.Configure<EmailIntegrationOptions>(configuration.GetSection("EmailIntegrations"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<CompanyIntelligenceOptions>(configuration.GetSection("CompanyIntelligence"));
        services.Configure<MatchingOptions>(configuration.GetSection("Matching"));
        services.Configure<CompanySearchOptions>(configuration.GetSection("Companies"));
        services.AddValidatorsFromAssemblyContaining<CreateApplicationRequestValidator>();
        services.AddCorsPolicy(configuration);

        return services;
    }

    private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

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

        // L2 cache backend (Redis) + HybridCache, which layers an in-process L1 (IMemoryCache)
        // in front of it automatically once an IDistributedCache is registered.
        services.AddStackExchangeRedisCache(o => o.Configuration = redisConnectionString);
        services.AddHybridCache();

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
            .AddDefaultTokenProviders()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

        // Both the web app's JWT access token and the browser extension's PAT (Sprint 9) arrive
        // as a plain `Authorization: Bearer <value>` header — this policy scheme is the default
        // and forwards to whichever real scheme matches, purely by inspecting the token's shape
        // (PersonalAccessTokenDefaults.TokenPrefix), so every existing RequireAuthorization()
        // call site keeps working unchanged for both credential types.
        const string smartBearerScheme = "SmartBearer";

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = smartBearerScheme;
                options.DefaultChallengeScheme = smartBearerScheme;
            })
            .AddPolicyScheme(smartBearerScheme, smartBearerScheme, policyOptions =>
            {
                policyOptions.ForwardDefaultSelector = context =>
                {
                    var authorizationHeader = context.Request.Headers.Authorization.ToString();
                    return authorizationHeader.Contains(PersonalAccessTokenDefaults.TokenPrefix, StringComparison.Ordinal)
                        ? PersonalAccessTokenDefaults.AuthenticationScheme
                        : JwtBearerDefaults.AuthenticationScheme;
                };
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
            })
            .AddScheme<AuthenticationSchemeOptions, PersonalAccessTokenAuthenticationHandler>(
                PersonalAccessTokenDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorization();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPersonalAccessTokenService, PersonalAccessTokenService>();

        return services;
    }

    private static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICompanyResolver, CompanyResolver>();
        services.AddScoped<ICompanySearchService, CompanySearchService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IJobResolver, JobResolver>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IProductMetricsService, ProductMetricsService>();
        services.AddScoped<IGmailClient, GmailClient>();
        services.AddScoped<IEmailIntegrationService, EmailIntegrationService>();
        services.AddScoped<IJobMatchingProvider, OpenAiJobMatchingProvider>();
        services.AddScoped<IJobMatchingService, JobMatchingService>();
        services.AddScoped<ICompanyIntelligenceService, CompanyIntelligenceService>();

        return services;
    }

    private static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Postgres when running via docker-compose.");

        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(postgresConnectionString)));
        services.AddHangfireServer();

        return services;
    }
}
