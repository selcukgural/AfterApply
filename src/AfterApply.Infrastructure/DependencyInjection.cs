using System.Reflection;
using AfterApply.Application.Analytics;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Companies;
using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.Identity;
using AfterApply.Application.Imports;
using AfterApply.Application.Metrics;
using AfterApply.Application.Notifications;
using AfterApply.Application.TrackedJobs;
using AfterApply.Infrastructure.Analytics;
using AfterApply.Infrastructure.Applications;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.CompanyIntelligence;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Imports;
using AfterApply.Infrastructure.Metrics;
using AfterApply.Infrastructure.OpenAi;
using AfterApply.Infrastructure.Notifications;
using AfterApply.Infrastructure.Persistence;
using AfterApply.Infrastructure.TrackedJobs;
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
    public const string InboundEmailRateLimitPolicy = "inbound-email";

    // dotnet build's OpenAPI GetDocument step (postman/scripts/generate-collection.js's
    // input) runs this entrypoint via a mock server that never serves real traffic, so it
    // never needs a working Postgres/Redis/JWT signing key — but AddInfrastructure's
    // fail-fast config checks below would otherwise block every `dotnet build`, everywhere,
    // the moment those env vars aren't set. Detected the same way Program.cs would, so
    // both stay in sync without one depending on the other's flag.
    public static readonly bool IsOpenApiDocumentGeneration =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLocalization();
        services.AddPersistence(configuration);
        services.AddIdentityAndJwt(configuration);
        services.AddApplicationServices();
        services.AddBackgroundJobs(configuration);

        // Persisting keys to the DB needs a working connection to read the existing key ring
        // at startup, which the placeholder connection string above can't provide — skip it
        // during OpenAPI generation and let DataProtection fall back to its ephemeral default.
        if (!IsOpenApiDocumentGeneration)
        {
            services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();
        }

        services.Configure<ImportOptions>(configuration.GetSection("Imports"));
        services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
        services.Configure<EmailForwardingOptions>(configuration.GetSection("EmailForwarding"));
        services.Configure<JobBoardDomainsOptions>(configuration.GetSection("JobBoardDomains"));

        // AddOptions().Bind().ValidateOnStart() (not the bare Configure<T> other sections above use)
        // so EmailIntelligenceConfigurationValidator actually runs during host startup and fails fast
        // on a missing weight/phrase — see EmailIntelligenceOptions' own doc comment for why.
        services.AddSingleton<IValidateOptions<EmailIntelligenceOptions>, EmailIntelligenceConfigurationValidator>();
        services.AddOptions<EmailIntelligenceOptions>()
            .Bind(configuration.GetSection("EmailIntelligence"))
            .ValidateOnStart();
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<CompanyIntelligenceOptions>(configuration.GetSection("CompanyIntelligence"));
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
            ?? (IsOpenApiDocumentGeneration ? "Host=localhost;Database=openapi-gen;Username=openapi-gen;Password=openapi-gen" : null)
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Postgres when running via docker-compose.");

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? (IsOpenApiDocumentGeneration ? "localhost:6379" : null)
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
            ?? (IsOpenApiDocumentGeneration ? Convert.ToBase64String("openapi-generation-placeholder-key-32-bytes!!"u8.ToArray()) : null)
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
                options.Events = new JwtBearerEvents
                {
                    // SignalR's browser client can't set an Authorization header on the
                    // WebSocket handshake, so it sends the token as ?access_token=... instead
                    // (its accessTokenFactory default). Only honor that for the hub path.
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
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
        services.AddScoped<ITrackedJobService, TrackedJobService>();
        services.AddHttpClient<IJobLinkPreviewService, JobLinkPreviewService>(client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient<ICompanyEnrichmentService, CompanyEnrichmentService>(client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IJobResolver, JobResolver>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IProductMetricsService, ProductMetricsService>();
        services.AddScoped<IEmailClassificationProvider, OpenAiEmailClassificationProvider>();
        services.AddScoped<IEmailJobExtractionProvider, OpenAiEmailJobExtractionProvider>();
        services.AddScoped<IEmailForwardingService, EmailForwardingService>();
        services.AddSingleton<IJobBoardDomainMatcher, JobBoardDomainMatcher>();
        services.AddScoped<ICompanyIntelligenceService, CompanyIntelligenceService>();

        return services;
    }

    private static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? (IsOpenApiDocumentGeneration ? "Host=localhost;Database=openapi-gen;Username=openapi-gen;Password=openapi-gen" : null)
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Postgres when running via docker-compose.");

        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(postgresConnectionString)));

        // The server is a background worker that immediately polls storage on start — pointless
        // (and, against the placeholder connection string above, noisy) during OpenAPI
        // generation, which never runs any job and exits right after the doc is written.
        if (!IsOpenApiDocumentGeneration)
        {
            // Default ShutdownTimeout (15s) was observed to be too tight for a
            // WebApplicationFactory-hosted integration test under concurrent load: Hangfire's
            // watchdog/heartbeat/dispatcher components still need to wind down gracefully even
            // when no job is actually running, and under CPU contention from many concurrent test
            // hosts that alone can exceed 15s — surfacing as a TaskCanceledException out of
            // WaitForShutdownAsync during the test fixture's DisposeAsync. A more generous timeout
            // is harmless in production (a slow, graceful shutdown a few seconds longer than usual
            // costs nothing) and makes tests reliably wait it out instead of failing on teardown.
            services.AddHangfireServer(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        }

        return services;
    }
}
