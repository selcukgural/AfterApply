using System.Reflection;
using System.Text.Json;
using AfterApply.Application.Admin;
using AfterApply.Application.Analytics;
using AfterApply.Application.Auditing;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Companies;
using AfterApply.Application.Documents;
using AfterApply.Application.CompanyIntelligence;
using AfterApply.Application.ResponseRates;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.Identity;
using AfterApply.Application.Imports;
using AfterApply.Application.Mailing;
using AfterApply.Application.Metrics;
using AfterApply.Application.Benchmark;
using AfterApply.Application.SilenceReports;
using AfterApply.Application.Blog;
using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
using AfterApply.Application.SiteStats;
using AfterApply.Application.SiteTraffic;
using AfterApply.Application.Notifications;
using AfterApply.Application.CompanyReviews;
using AfterApply.Application.CandidateExperiences;
using AfterApply.Application.CompanySalaries;
using AfterApply.Application.Occupations;
using AfterApply.Application.Feedback;
using AfterApply.Application.TrackedJobs;
using AfterApply.Infrastructure.Admin;
using AfterApply.Infrastructure.Analytics;
using AfterApply.Infrastructure.Auditing;
using AfterApply.Infrastructure.Applications;
using AfterApply.Infrastructure.Companies;
using AfterApply.Infrastructure.CompanyIntelligence;
using AfterApply.Infrastructure.ResponseRates;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Imports;
using AfterApply.Application.AtsSources;
using AfterApply.Infrastructure.AtsSources;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.Pro;
using AfterApply.Application.JobSources;
using AfterApply.Application.Payments;
using AfterApply.Application.Pro;
using AfterApply.Infrastructure.Mailing;
using AfterApply.Infrastructure.Metrics;
using AfterApply.Infrastructure.Benchmark;
using AfterApply.Infrastructure.SilenceReports;
using AfterApply.Infrastructure.Blog;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Ai;
using AfterApply.Infrastructure.CvScan;
using AfterApply.Infrastructure.SiteStats;
using AfterApply.Infrastructure.SiteTraffic;
using AfterApply.Infrastructure.OpenAi;
using AfterApply.Infrastructure.Notifications;
using AfterApply.Infrastructure.Persistence;
using AfterApply.Infrastructure.CompanyReviews;
using AfterApply.Infrastructure.CandidateExperiences;
using AfterApply.Infrastructure.CompanySalaries;
using AfterApply.Infrastructure.Occupations;
using AfterApply.Infrastructure.Feedback;
using AfterApply.Infrastructure.TrackedJobs;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Google.Cloud.Storage.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace AfterApply.Infrastructure;

public static class DependencyInjection
{
    public const string LinkedInJobSourceResiliencePipeline = "linkedin-job-source";

    public const string AtsJobSourceResiliencePipeline = "ats-job-source";
    public const string KariyerNetJobSourceResiliencePipeline = "kariyernet-job-source";

    public const string CorsPolicyName = "Frontend";
    public const string AuthRateLimitPolicy = "auth-strict";
    public const string UploadRateLimitPolicy = "upload";
    public const string ExtensionSignalRateLimitPolicy = "extension-signal";
    public const string LinkPreviewRateLimitPolicy = "link-preview";
    public const string FeedbackRateLimitPolicy = "feedback";
    public const string PaymentCheckoutRateLimitPolicy = "payment-checkout";
    public const string CompanyReviewWriteRateLimitPolicy = "company-review-write";
    public const string CompanyReviewReportRateLimitPolicy = "company-review-report";
    public const string CompanyReviewHelpfulRateLimitPolicy = "company-review-helpful";

    public const string CompanySalaryHelpfulRateLimitPolicy = "company-salary-helpful";

    public const string CandidateExperienceHelpfulRateLimitPolicy = "candidate-experience-helpful";
    public const string CompanyPublicSearchRateLimitPolicy = "company-public-search";
    public const string CompanySalaryWriteRateLimitPolicy = "company-salary-write";
    public const string CandidateExperienceWriteRateLimitPolicy = "candidate-experience-write";
    public const string SiteTrafficRateLimitPolicy = "site-traffic";
    public const string BenchmarkRateLimitPolicy = "benchmark";

    public const string SilenceReportRateLimitPolicy = "silence-report";
    public const string CvScanRateLimitPolicy = "cv-scan";
    public const string ExtensionPairingStartRateLimitPolicy = "extension-pairing-start";
    public const string ExtensionPairingPollRateLimitPolicy = "extension-pairing-poll";
    public const string BlogLikeRateLimitPolicy = "blog-like";
    public const string BlogCommentWriteRateLimitPolicy = "blog-comment-write";
    public const string BlogCommentReportRateLimitPolicy = "blog-comment-report";
    public const string BlogCommentHelpfulRateLimitPolicy = "blog-comment-helpful";
    public const string BlogSeoSuggestRateLimitPolicy = "blog-seo-suggest";

    // dotnet build's OpenAPI GetDocument step (postman/scripts/generate-collection.js's
    // input) runs this entrypoint via a mock server that never serves real traffic, so it
    // never needs a working Postgres/JWT signing key — but AddInfrastructure's
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
        services.Configure<ApplicationBulkOptions>(configuration.GetSection(ApplicationBulkOptions.SectionName));
        services.Configure<IdentityPolicyOptions>(configuration.GetSection(IdentityPolicyOptions.SectionName));
        services.Configure<PersonalAccessTokenOptions>(configuration.GetSection(PersonalAccessTokenOptions.SectionName));
        services.Configure<CompanyProfileOptions>(configuration.GetSection(CompanyProfileOptions.SectionName));
        services.Configure<EmailVerificationOptions>(configuration.GetSection(EmailVerificationOptions.SectionName));
        services.Configure<ExtensionPairingOptions>(configuration.GetSection(ExtensionPairingOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
        services.Configure<ProductMetricsOptions>(configuration.GetSection(ProductMetricsOptions.SectionName));
        services.Configure<BenchmarkOptions>(configuration.GetSection(BenchmarkOptions.SectionName));
        services.Configure<SilenceReportOptions>(configuration.GetSection(SilenceReportOptions.SectionName));
        services.Configure<SiteStatsOptions>(configuration.GetSection(SiteStatsOptions.SectionName));
        services.Configure<CvScanOptions>(configuration.GetSection(CvScanOptions.SectionName));
        services.Configure<EmailForwardingOptions>(configuration.GetSection("EmailForwarding"));
        services.Configure<EmailAutoApprovalOptions>(configuration.GetSection("EmailAutoApproval"));
        services.Configure<JobBoardDomainsOptions>(configuration.GetSection("JobBoardDomains"));
        services.Configure<AppOptions>(configuration.GetSection("App"));
        services.Configure<ResendOptions>(configuration.GetSection("Resend"));
        services.Configure<FeedbackGitHubOptions>(configuration.GetSection(FeedbackGitHubOptions.SectionName));
        services.AddHttpClient<IEmailSender, ResendEmailSender>(client => client.BaseAddress = new Uri("https://api.resend.com/"));

        // AddOptions().Bind().ValidateOnStart() (not the bare Configure<T> other sections above use)
        // so EmailIntelligenceConfigurationValidator actually runs during host startup and fails fast
        // on a missing weight/phrase — see EmailIntelligenceOptions' own doc comment for why.
        services.AddSingleton<IValidateOptions<EmailIntelligenceOptions>, EmailIntelligenceConfigurationValidator>();
        services.AddOptions<EmailIntelligenceOptions>()
            .Bind(configuration.GetSection("EmailIntelligence"))
            .ValidateOnStart();
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<CompanyIntelligenceOptions>(configuration.GetSection("CompanyIntelligence"));
        services.Configure<ResponseRateOptions>(configuration.GetSection("ResponseRates"));
        services.Configure<CompanySearchOptions>(configuration.GetSection("Companies"));
        services.Configure<CompanyReviewOptions>(configuration.GetSection(CompanyReviewOptions.SectionName));
        services.Configure<CompanySalaryOptions>(configuration.GetSection(CompanySalaryOptions.SectionName));
        services.Configure<CandidateExperienceOptions>(configuration.GetSection(CandidateExperienceOptions.SectionName));
        services.Configure<OccupationSearchOptions>(configuration.GetSection(OccupationSearchOptions.SectionName));
        services.Configure<RequestAuditOptions>(configuration.GetSection(RequestAuditOptions.SectionName));
        services.Configure<JobSourceOptions>(configuration.GetSection(JobSourceOptions.SectionName));
        services.Configure<AtsSourceOptions>(configuration.GetSection(AtsSourceOptions.SectionName));
        services.Configure<BlogOptions>(configuration.GetSection(BlogOptions.SectionName));
        services.AddPayments(configuration);
        services.AddDocumentStorage(configuration);
        services.AddBlog(configuration);
        services.AddValidatorsFromAssemblyContaining<CreateApplicationRequestValidator>();
        services.AddCorsPolicy(configuration);

        return services;
    }

    /// <summary>
    /// Wires up CV file storage. The provider is configuration, not compilation, so the test suite
    /// and a fresh clone can run against a directory while the deployed service talks to Cloud
    /// Storage — but Production is not allowed to pick the directory: Cloud Run's filesystem is
    /// in-memory and per-instance, so an upload written there would vanish on the next revision and
    /// be invisible to every other instance. Failing at startup is the only way that mistake
    /// surfaces before a user's CV is silently lost.
    /// </summary>
    private static IServiceCollection AddDocumentStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
            ?? new StorageOptions();
        var isProduction = string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Production",
            StringComparison.OrdinalIgnoreCase);

        if (storageOptions.Provider == FileStorageProvider.GoogleCloudStorage)
        {
            if (string.IsNullOrWhiteSpace(storageOptions.BucketName) && !IsOpenApiDocumentGeneration)
            {
                throw new InvalidOperationException(
                    "Storage:BucketName is required when Storage:Provider is GoogleCloudStorage. " +
                    "Set Storage__BucketName to the CV bucket's name (see DEPLOYMENT.md).");
            }

            // Singleton: StorageClient is thread-safe and holds a pooled HttpClient, so one per
            // process is both correct and what avoids a socket per request.
            services.AddSingleton(_ =>
            {
                var builder = new StorageClientBuilder();

                if (!string.IsNullOrWhiteSpace(storageOptions.EmulatorBaseUri))
                {
                    builder.BaseUri = storageOptions.EmulatorBaseUri;
                    builder.UnauthenticatedAccess = true;
                }

                return builder.Build();
            });

            services.AddScoped<IFileStorage, GoogleCloudStorageFileStorage>();
        }
        else
        {
            if (isProduction)
            {
                throw new InvalidOperationException(
                    "Storage:Provider must be GoogleCloudStorage in Production — Cloud Run's " +
                    "filesystem is in-memory and per-instance, so FileSystem storage would lose " +
                    "every uploaded CV. See DEPLOYMENT.md.");
            }

            services.AddScoped<IFileStorage, FileSystemFileStorage>();
        }

        services.AddScoped<ICvDocumentService, CvDocumentService>();

        return services;
    }

    /// <summary>
    /// The blog (DECISIONS.md 2026-09-19). Its images get a storage binding of their own over the
    /// same two implementations as the CVs — a second bucket (or directory), never a prefix in the
    /// CV one, so a public image and a private document can never share an access policy by
    /// accident. Same Production rule as <see cref="AddDocumentStorage"/>: Cloud Storage or fail
    /// at startup, and the bucket name has to be there while the feature is on.
    /// </summary>
    private static IServiceCollection AddBlog(this IServiceCollection services, IConfiguration configuration)
    {
        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
            ?? new StorageOptions();
        var blogOptions = configuration.GetSection(BlogOptions.SectionName).Get<BlogOptions>() ?? new BlogOptions();

        if (storageOptions.Provider == FileStorageProvider.GoogleCloudStorage)
        {
            if (blogOptions.Enabled && string.IsNullOrWhiteSpace(storageOptions.BlogMediaBucketName)
                && !IsOpenApiDocumentGeneration)
            {
                throw new InvalidOperationException(
                    "Storage:BlogMediaBucketName is required when Storage:Provider is GoogleCloudStorage and " +
                    "Blog:Enabled is true. Set Storage__BlogMediaBucketName to the blog media bucket's name " +
                    "(see DEPLOYMENT.md), or switch the blog off.");
            }

            // The StorageClient singleton is registered by AddDocumentStorage.
            services.AddScoped<IBlogMediaStorage>(sp =>
                new GoogleCloudStorageFileStorage(sp.GetRequiredService<StorageClient>(),
                    sp.GetRequiredService<IOptions<StorageOptions>>().Value.BlogMediaBucketName));
        }
        else
        {
            // Production is already refused by AddDocumentStorage for this provider.
            services.AddScoped<IBlogMediaStorage>(sp =>
                new FileSystemFileStorage(sp.GetRequiredService<IOptions<StorageOptions>>().Value.BlogLocalRootPath));
        }

        // Built once: the allowlists are fixed and the sanitizer is thread-safe after construction.
        services.AddSingleton<IBlogHtmlSanitizer, BlogHtmlSanitizer>();
        services.AddScoped<IBlogCacheInvalidator, BlogCacheInvalidator>();
        services.AddScoped<IBlogAdminService, BlogAdminService>();
        // The SEO suggestion (2026-09-21): the same Vertex client as the CV review, its own named
        // HttpClient so its timeout is its own.
        services.AddScoped<IBlogSeoSuggestionProvider, VertexBlogSeoSuggestionProvider>();
        services.AddHttpClient(BlogOptions.BlogSeoSettings.HttpClientName);
        services.AddScoped<IBlogPublicService, BlogPublicService>();
        services.AddScoped<IBlogCommentService, BlogCommentService>();
        services.AddScoped<IBlogMediaService, BlogMediaService>();

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
        var postgresConnectionString = PostgresConnectionString.Resolve(configuration, IsOpenApiDocumentGeneration);

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnectionString));

        // Two-level cache (DECISIONS.md 2026-09-18 "Redis geri geldi"): the DI MemoryCache is the
        // in-process L1, Memorystore Redis is the shared L2, and a Redis pub/sub backplane carries
        // every Remove/Set/RemoveByTag from the instance that made it to every other Cloud Run
        // instance's L1. That last part is why this is FusionCache rather than Microsoft's own
        // AddHybridCache(): DefaultHybridCache never consults L2 while an L1 entry exists and reads a
        // tag's invalidation time from L2 exactly once, so with two or more instances an eviction on
        // one of them left the others serving the old value until their L1 TTL lapsed — the bug of
        // 2026-09-18 (a candidate experience written on one instance, the company summary stale on
        // another). The services keep depending on the HybridCache abstract class; AsHybridCache()
        // registers FusionCache as its implementation, so none of them changed.
        //
        // Redis being unreachable degrades rather than fails: the soft/hard timeouts bound how long a
        // request waits on L2, the circuit breakers stop trying for a while, auto-recovery replays the
        // backplane traffic that was missed, and the health check reports Degraded (200), not
        // Unhealthy. Fail-safe (serving a stale entry when the factory throws) stays off so a DB error
        // surfaces the way it does today.
        //
        // SizeLimit counts entries, not bytes: FusionCache stamps each L1 entry with
        // DefaultEntryOptions.Size (1 below) — a MemoryCache with a SizeLimit throws on any entry
        // that has none — and the bound is still needed because company-search:{query} has
        // user-supplied cardinality. 10k entries is far above the working set and far below the
        // container's memory, so it only engages on an abusive burst.
        var cachingOptions = configuration.GetSection(CachingOptions.SectionName).Get<CachingOptions>() ?? new CachingOptions();
        var redisConnectionString = RedisConnectionString.Resolve(configuration, IsOpenApiDocumentGeneration);
        services.Configure<CachingOptions>(configuration.GetSection(CachingOptions.SectionName));

        // One multiplexer for everything that talks to Redis (L2, backplane, health check, and the
        // rate limiter/SignalR/locks that follow), created on first use. Connect (sync) is the only
        // API StackExchange.Redis offers for a lazily-built singleton in a DI factory — the
        // composition-root exception to the async rule; with abortConnect=false in the connection
        // string it returns at once and keeps retrying in the background if Redis is down.
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddMemoryCache(options => options.SizeLimit = 10_000);
        services.AddFusionCache()
            .WithOptions(options =>
            {
                options.CacheKeyPrefix = cachingOptions.KeyPrefix;
                options.BackplaneChannelPrefix = cachingOptions.ChannelPrefix;
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(30);
                options.BackplaneCircuitBreakerDuration = TimeSpan.FromSeconds(30);
                options.EnableAutoRecovery = true;
                options.WaitForInitialBackplaneSubscribe = cachingOptions.WaitForBackplaneSubscribe;
            })
            .WithDefaultEntryOptions(options =>
            {
                options.Size = 1;
                // An in-region Memorystore read takes about a millisecond; past the soft timeout
                // the factory runs and the L2 result is applied in the background, past the hard
                // one the L2 call is abandoned. Measured 2026-09-18 with Redis stopped: a request
                // that nests two cache reads (company page → summary) waited 2 × hard timeout once
                // before the circuit breaker opened, so the hard limit is kept at a second.
                options.DistributedCacheSoftTimeout = TimeSpan.FromMilliseconds(200);
                options.DistributedCacheHardTimeout = TimeSpan.FromSeconds(1);
                options.AllowBackgroundDistributedCacheOperations = true;
                options.ReThrowDistributedCacheExceptions = false;
                options.ReThrowBackplaneExceptions = false;
                options.IsFailSafeEnabled = false;
            })
            .WithRegisteredMemoryCache()
            // IncludeFields so the serializer can also carry value tuples; nothing cached today
            // holds one, but FusionCache probes for it at start-up and warns on every boot
            // otherwise.
            .WithSerializer(new FusionCacheSystemTextJsonSerializer(new JsonSerializerOptions { IncludeFields = true }))
            .WithDistributedCache(sp => new RedisCache(new RedisCacheOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
            }))
            // The backplane's pub/sub on a connection of its own (2026-09-20), the one exception
            // to the shared multiplexer above: RedisBackplane disposes whatever connection it
            // holds when FusionCache is disposed, factory-provided or not (Disconnect →
            // _muxer.Dispose()), and on the shared one that closed Redis under everything the
            // container disposed after it — the SignalR hub manager's own Dispose then threw
            // ObjectDisposedException out of Host.Dispose (a CI run in three, 2026-09-19/20).
            // Owning its connection, the backplane can only close its own.
            .WithBackplane(_ => new RedisBackplane(new RedisBackplaneOptions { Configuration = redisConnectionString }))
            .AsHybridCache();
        services.AddScoped<ICompanyCacheInvalidator, CompanyCacheInvalidator>();

        // Cross-instance mutex, on the same multiplexer. Deliberately narrow in use: Hangfire's
        // recurring jobs, the unique indexes and the CV advisory lock already serialise what
        // Postgres can serialise; this is for work Postgres never sees, such as an outbound fetch
        // two workers would otherwise both make (CompanyEnrichmentService). Lock names carry the
        // key prefix so two deployments on one Redis cannot block each other.
        services.AddSingleton<IDistributedLockProvider>(sp =>
            new RedisDistributedSynchronizationProvider(sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase()));
        services.AddSingleton(new DistributedLockNames(cachingOptions.KeyPrefix));

        services.AddHealthChecks()
            .AddNpgSql(postgresConnectionString, name: "postgres")
            .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis", failureStatus: HealthStatus.Degraded);

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

        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<LocalizedIdentityErrorDescriber>();

        // The password/lockout policy comes from the "Identity" configuration section (see
        // IdentityPolicyOptions for the defaults and their rationale) so that tightening it is a
        // config change, not a redeploy. Registered as a second IConfigureOptions<IdentityOptions>
        // rather than inside the AddIdentityCore lambda because it needs the bound options, and it
        // runs after Identity's own defaults so it always wins. GET /api/config republishes the
        // resulting IdentityOptions.Password — the exact object PasswordValidator enforces — so the
        // web app and the server can never disagree about the rules.
        //
        // Only new/changed passwords are affected: sign-in never re-evaluates the policy, so
        // existing accounts keep working after a change.
        //
        // Lockout is what actually bounds per-account password guessing — the control the IP-based
        // auth rate limiter can't provide on its own (an attacker spread across many IPs still hits
        // this). It applies from the first failure because CreateAsync sets LockoutEnabled from
        // Lockout.AllowedForNewUsers.
        services.AddOptions<IdentityOptions>().Configure<IOptions<IdentityPolicyOptions>>((options, policy) =>
        {
            var password = policy.Value.Password;
            options.Password.RequiredLength = password.RequiredLength;
            options.Password.RequiredUniqueChars = password.RequiredUniqueChars;
            options.Password.RequireDigit = password.RequireDigit;
            options.Password.RequireLowercase = password.RequireLowercase;
            options.Password.RequireUppercase = password.RequireUppercase;
            options.Password.RequireNonAlphanumeric = password.RequireNonAlphanumeric;

            var lockout = policy.Value.Lockout;
            options.Lockout.MaxFailedAccessAttempts = lockout.MaxFailedAccessAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockout.LockoutMinutes);
            options.Lockout.AllowedForNewUsers = true;
        });

        // Default token lifespan is 1 day — too long for a password-reset link. Shared by every
        // "Default"-purpose token provider (there's no separate email-confirmation flow today to
        // conflict with).
        services.AddOptions<DataProtectionTokenProviderOptions>().Configure<IOptions<IdentityPolicyOptions>>(
            (options, policy) => options.TokenLifespan = TimeSpan.FromMinutes(policy.Value.PasswordResetTokenMinutes));

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
                // StartsWith, not Contains: a JWT's payload is base64url, which includes '_', and
                // it encodes attacker-influenced values (the email claim). A crafted address whose
                // encoding happens to contain "aa_pat_" anywhere in the token would route that
                // user's perfectly valid JWT to the PAT handler and fail every request they make.
                // The prefix only means anything at the very front of the credential anyway.
                policyOptions.ForwardDefaultSelector = context =>
                {
                    var authorizationHeader = context.Request.Headers.Authorization.ToString();
                    return authorizationHeader.StartsWith(
                        $"Bearer {PersonalAccessTokenDefaults.TokenPrefix}", StringComparison.OrdinalIgnoreCase)
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

        // The scope requirement goes on the DEFAULT policy, not on individual endpoints, so that an
        // endpoint added later is out of an Extension-scoped token's reach until someone explicitly
        // calls .AllowExtensionToken() on it. See PersonalAccessTokenScopeHandler.
        services.AddSingleton<IAuthorizationHandler, PersonalAccessTokenScopeHandler>();
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PersonalAccessTokenScopeRequirement())
                .Build();
        });

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<CompanyVisibility>();
        services.AddScoped<AuthEmailThrottle>();
        services.AddScoped<EmailVerificationService>();
        services.AddScoped<IEmailVerificationCodeSender, EmailVerificationCodeSender>();
        services.AddScoped<IUnverifiedAccountCleanupService, UnverifiedAccountCleanupService>();
        services.AddScoped<IPersonalAccessTokenService, PersonalAccessTokenService>();
        services.AddScoped<IExtensionPairingService, ExtensionPairingService>();

        // Sign in with Google. Inert (button hidden, endpoints 404) until GoogleAuth:ClientId and
        // GoogleAuth:ClientSecret are both set — see GoogleAuthOptions.
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddHttpClient<IGoogleAuthClient, GoogleAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10));

        // Sign in with LinkedIn. Inert (button hidden, endpoints 404) until LinkedInAuth:ClientId and
        // LinkedInAuth:ClientSecret are both set — see LinkedInAuthOptions. Unlike Google, the ID
        // token's signature is fully verified against LinkedIn's published JWKS (LinkedInJwksProvider
        // is a singleton so its 24h cache actually persists across requests, on its own named
        // HttpClient rather than the typed one below).
        services.Configure<LinkedInAuthOptions>(configuration.GetSection(LinkedInAuthOptions.SectionName));
        services.AddHttpClient("LinkedInJwks", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton(sp =>
            new LinkedInJwksProvider(sp.GetRequiredService<IHttpClientFactory>().CreateClient("LinkedInJwks")));
        services.AddHttpClient<ILinkedInAuthClient, LinkedInAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10));

        // Sign in with GitHub. Inert (button hidden, endpoints 404) until GitHubAuth:ClientId and
        // GitHubAuth:ClientSecret are both set — see GitHubAuthOptions. No JWKS provider here
        // because there is no id_token to verify: GitHub's OAuth Apps issue none, so the identity
        // is read over TLS from api.github.com (see GitHubAuthClient).
        services.Configure<GitHubAuthOptions>(configuration.GetSection(GitHubAuthOptions.SectionName));
        services.AddHttpClient<IGitHubAuthClient, GitHubAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10));

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
        services.AddScoped<ContributionNotificationWriter>();
        services.AddScoped<INotificationFeedService, NotificationFeedService>();
        services.AddScoped<IContributionNotificationRetentionService, ContributionNotificationRetentionService>();
        services.AddScoped<IProductMetricsService, ProductMetricsService>();
        services.AddScoped<IRequestAuditRetentionService, RequestAuditRetentionService>();
        services.AddScoped<ISiteTrafficService, SiteTrafficService>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();
        services.AddScoped<ISilenceReportService, SilenceReportService>();
        services.AddScoped<ISiteStatsService, SiteStatsService>();
        services.AddScoped<ICvScanService, CvScanService>();
        services.AddScoped<ICvTextExtractor, CvTextExtractor>();
        services.AddSingleton<IVertexGenerateContentClient, VertexGenerateContentClient>();
        services.AddScoped<ICvReviewProvider, VertexCvReviewProvider>();
        services.AddJobSources();

        // A named client so the timeout and the handler lifetime belong to this call rather than to
        // whatever DefaultClient happens to be configured with. No BaseAddress: the host carries
        // the Vertex region, and building it per request is what keeps that visible at the call
        // site (see VertexCvReviewProvider).
        services.AddHttpClient(CvScanOptions.ReviewHttpClientName);
        services.AddScoped<IAdminAccessService, AdminAccessService>();
        services.AddScoped<IAutoApprovalCalibrationService, AutoApprovalCalibrationService>();
        services.AddScoped<IEmailClassificationProvider, OpenAiEmailClassificationProvider>();
        services.AddScoped<IEmailJobExtractionProvider, OpenAiEmailJobExtractionProvider>();
        services.AddScoped<IEmailRejectionReasonExtractionProvider, OpenAiEmailRejectionReasonExtractionProvider>();
        services.AddScoped<IEmailForwardingService, EmailForwardingService>();
        services.AddScoped<ILocalFilterConfigService, LocalFilterConfigService>();
        services.AddSingleton<IJobBoardDomainMatcher, JobBoardDomainMatcher>();
        services.AddScoped<ICompanyIntelligenceService, CompanyIntelligenceService>();
        services.AddScoped<ISectorResponseRateService, SectorResponseRateService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<CompanySlugAllocator>();
        services.AddScoped<CompanyReviewQueries>();
        services.AddScoped<ContributionProofQueries>();
        services.AddScoped<ICompanyReviewService, CompanyReviewService>();
        services.AddScoped<ICompanyDirectoryService, CompanyDirectoryService>();
        services.AddScoped<ICompanyReviewModerationService, CompanyReviewModerationService>();
        services.AddScoped<ICompanySalaryService, CompanySalaryService>();
        services.AddScoped<ICompanySalaryAdminService, CompanySalaryAdminService>();
        services.AddScoped<CandidateExperienceQueries>();
        services.AddScoped<ICandidateExperienceService, CandidateExperienceService>();
        services.AddScoped<ICandidateExperienceAdminService, CandidateExperienceAdminService>();
        services.AddScoped<IExperienceInviteService, ExperienceInviteService>();
        services.AddScoped<ICompanyContributionService, CompanyContributionService>();
        services.AddScoped<IOccupationSearchService, OccupationSearchService>();
        services.AddHttpClient<IGitHubIssueMirror, GitHubIssueMirror>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            // GitHub rejects requests with no User-Agent; it wants something identifying.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("e-kariyerim-feedback-mirror");
        });

        return services;
    }

    private static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        // Validated on start when enabled: an empty merchant key or a zero price must not reach a
        // customer's checkout (PayTrOptionsValidator).
        services.AddSingleton<IValidateOptions<PayTrOptions>, PayTrOptionsValidator>();
        services.AddOptions<PayTrOptions>().Bind(configuration.GetSection(PayTrOptions.SectionName)).ValidateOnStart();
        services.AddScoped<IPaymentCheckoutService, PaymentCheckoutService>();
        services.AddScoped<IPayTrCallbackService, PayTrCallbackService>();
        services.AddScoped<IPaymentRefundService, PaymentRefundService>();
        services.AddScoped<IPaymentAdminService, PaymentAdminService>();
        services.AddScoped<IPaymentMaintenanceService, PaymentMaintenanceService>();
        // Both PayTR calls are short form POSTs; 15 s is well above their normal answer and short
        // enough that a stuck call does not hold the user's request for long. No retry handler:
        // a refund must never be repeated by a machine.
        services.AddHttpClient<IPayTrClient, PayTrClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.paytr.com/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }

    private static IServiceCollection AddJobSources(this IServiceCollection services)
    {
        services.AddScoped<IProEntitlementService, ProEntitlementService>();
        services.AddScoped<IUserJobSourceProfileService, UserJobSourceProfileService>();
        services.AddScoped<IUserJobSourceDeliveryService, UserJobSourceDeliveryService>();
        services.AddScoped<IJobSourceAdminService, JobSourceAdminService>();
        services.AddScoped<IJobSourceSweepService, JobSourceSweepService>();
        services.AddScoped<IJobFitScoringService, JobFitScoringService>();
        services.AddScoped<IJobSourceDigestService, JobSourceDigestService>();
        services.AddScoped<IJobFitScoringProvider, VertexJobFitScoringProvider>();
        services.AddScoped<IUserCvTextReader, StoredCvTextReader>();
        // Same shape as the CV scan's review client: a bare named client, the URL built per call
        // so the region stays visible at the call site (VertexGenerateContentClient).
        services.AddHttpClient(JobFitScoringSettings.HttpClientName);

        // The search text is in the request URL, so the default request/response logging is
        // removed — a user's job title and city are theirs, not the log's. The pipeline retries
        // only what a network can cause (a dropped connection, a per-attempt timeout, a 5xx); a
        // 429 or 403 from LinkedIn is not handled here on purpose, because retrying into a rate
        // limit is how a low-volume client gets itself blocked — the client reports it and the
        // sweep stops for a day (JobSourceBudget). The in-process breaker is a short local guard;
        // the durable one is in the ledger.
        services.AddHttpClient<ILinkedInJobSourceClient, LinkedInJobSourceClient>(client =>
            {
                // Above the pipeline's total timeout, so the pipeline is what gives up, not the client.
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddResilienceHandler(LinkedInJobSourceResiliencePipeline, (pipeline, context) =>
                JobSourceResilience.Configure(pipeline,
                    context.ServiceProvider.GetRequiredService<IOptions<JobSourceOptions>>().Value));

        // kariyer.net, the same way: its own client and pipeline, so one site's breaker never
        // trips for the other. The sweep asks for both through IEnumerable<IJobSourceClient>.
        services.AddHttpClient<IKariyerNetJobSourceClient, KariyerNetJobSourceClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddResilienceHandler(KariyerNetJobSourceResiliencePipeline, (pipeline, context) =>
                JobSourceResilience.Configure(pipeline,
                    context.ServiceProvider.GetRequiredService<IOptions<JobSourceOptions>>().Value));
        services.AddScoped<IJobSourceClient>(sp => sp.GetRequiredService<ILinkedInJobSourceClient>());
        services.AddScoped<IJobSourceClient>(sp => sp.GetRequiredService<IKariyerNetJobSourceClient>());

        // The ATS posting APIs: one client for all five, because they are public JSON endpoints
        // with none of the per-site behaviour the two scraped sites have. Its own pipeline all the
        // same, so a breaker tripped by an ATS outage never stops the sweep, or the reverse.
        services.AddHttpClient<IAtsJobClient, AtsJobClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddResilienceHandler(AtsJobSourceResiliencePipeline, (pipeline, context) =>
                JobSourceResilience.Configure(pipeline,
                    context.ServiceProvider.GetRequiredService<IOptions<JobSourceOptions>>().Value));
        services.AddScoped<IAtsJobEnrichmentService, AtsJobEnrichmentService>();

        return services;
    }

    private static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = PostgresConnectionString.Resolve(configuration, IsOpenApiDocumentGeneration);

        // Same resolved string as AddPersistence, deliberately: Npgsql pools per connection string,
        // so this is what puts Hangfire's connections under the one Postgres:MaxPoolSize cap the
        // API's DbContext is under, instead of giving it a second pool of its own.
        services.AddHangfire(config => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(postgresConnectionString), new PostgreSqlStorageOptions
            {
                // A job's invisibility window (30 min by default) is extended while it is still
                // running instead of expiring at a fixed point: without this a sweep that ran past
                // thirty minutes — plausible with its politeness delays — was re-fetched by a
                // worker on another instance and ran twice. Postgres-backed, no Redis involved.
                UseSlidingInvisibilityTimeout = true
            }));

        // The server is a background worker that immediately polls storage on start — pointless
        // (and, against the placeholder connection string above, noisy) during OpenAPI
        // generation, which never runs any job and exits right after the doc is written.
        //
        // Hangfire:ServerEnabled exists for the same reason one step further out. It defaults to
        // true, so production and local development are unchanged, and the integration suite turns
        // it off for every host that does not actually need a job to run: shutting one of these
        // servers down is what destabilised that suite for weeks (Hangfire.PostgreSql's
        // ExpirationManager does not observe the shutdown token, so WaitForShutdownAsync burns its
        // whole budget and then reports "stopped non-gracefully" — surfacing as a
        // TaskCanceledException out of a fixture's DisposeAsync, a run that hangs, or a crashed test
        // host). A host that never starts a server has nothing to wind down. See
        // TestContainerCleanup.DisableHangfireServerForTests.
        var serverEnabled = configuration.GetValue("Hangfire:ServerEnabled", true);

        if (!IsOpenApiDocumentGeneration && serverEnabled)
        {
            // Both values keep their previous behaviour when unconfigured; they are settable so the
            // integration suite can ask for something cheaper — and, for WorkerCount, so production
            // can too: deploy.yml sets Hangfire__WorkerCount=2 because each worker polls the queue
            // on its own connection and the default of ProcessorCount × 5 (10 on Cloud Run) across
            // several instances is what was exhausting the db-f1-micro's 25 connection slots
            // (DECISIONS.md 2026-09-12 "53300 remaining connection slots").
            //
            // ShutdownTimeout was already raised from Hangfire's 15s default to 30s for the tests'
            // benefit, because WaitForShutdownAsync was timing out during a fixture's DisposeAsync
            // and failing the test. That treated the symptom and did not work: the suite still
            // fails there, and depending on timing the same stall shows up as a hung run or an
            // outright test-host crash instead of a failed test.
            //
            // The cause is volume. A test host builds a WebApplicationFactory per test (xunit
            // constructs the class once per test method) and some classes build three, so a single
            // run starts and stops on the order of 200 Hangfire servers — each opening
            // min(ProcessorCount * 5, 20) workers plus watchdogs it then has to wind down. No test
            // needs twenty workers to observe one job run. WorkerCount is the lever that actually
            // reduces the work; the timeout only decides how long we wait for it.
            var shutdownTimeoutSeconds = configuration.GetValue("Hangfire:ShutdownTimeoutSeconds", 30);
            var workerCount = configuration.GetValue<int?>("Hangfire:WorkerCount");

            services.AddHangfireServer(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(shutdownTimeoutSeconds);

                if (workerCount is > 0)
                {
                    options.WorkerCount = workerCount.Value;
                }
            });
        }

        return services;
    }
}
