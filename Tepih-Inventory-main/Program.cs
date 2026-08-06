using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Inventar.Data;
using Inventar.Helpers;
using Inventar.Interfaces;
using Inventar.Middleware;
using Inventar.Models;
using Inventar.Repository;
using Inventar.Services;
using Inventar.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

if (args.Length > 0)
{
    builder.Configuration.AddCommandLine(args);
}

var secureCookiePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;
var reverseProxySettings = builder.Configuration.GetSection("ReverseProxy").Get<ReverseProxySettings>() ?? new ReverseProxySettings();
var hostRedirectSettings = builder.Configuration.GetSection(HostRedirectSettings.SectionName).Get<HostRedirectSettings>() ?? new HostRedirectSettings();
var inventarConnectionString = builder.Configuration.GetConnectionString("Inventar");

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITepihRepository, TepihRepository>();
builder.Services.AddScoped<IKupacRepository, KupacRepository>();
builder.Services.AddScoped<ISalesRepository, SalesRepository>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IPlacanjeRepository, PlacanjeRepository>();
builder.Services.AddScoped<IDugRepository, DugRepository>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IWebOrderProcessingService, WebOrderProcessingService>();
builder.Services.AddScoped<StorefrontPoMjeriAllocationService>();
builder.Services.AddScoped<StorefrontOrderAdminService>();
builder.Services.AddHostedService<ExpiredReservationCleanupService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddOptions<CloudinarySettings>()
    .Bind(builder.Configuration.GetSection("CloudinarySettings"))
    .Validate(
        settings => !builder.Environment.IsProduction() ||
            (!string.IsNullOrWhiteSpace(settings.CloudName) &&
             !string.IsNullOrWhiteSpace(settings.ApiKey) &&
             !string.IsNullOrWhiteSpace(settings.ApiSecret)),
        "CloudinarySettings must be fully configured in production.")
    .ValidateOnStart();
builder.Services.AddOptions<SendGridSettings>()
    .Bind(builder.Configuration.GetSection(SendGridSettings.SectionName));
builder.Services.AddOptions<HostRedirectSettings>()
    .Bind(builder.Configuration.GetSection(HostRedirectSettings.SectionName))
    .Validate(
        settings => !settings.Enabled || settings.HasValidConfiguration(),
        "HostRedirect must define DestinationHost and at least one SourceHost when enabled.")
    .ValidateOnStart();

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
}
else if (!Path.IsPathRooted(dataProtectionKeysPath))
{
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, dataProtectionKeysPath);
}

var dataProtectionApplicationName = builder.Configuration["DataProtection:ApplicationName"];
if (string.IsNullOrWhiteSpace(dataProtectionApplicationName))
{
    dataProtectionApplicationName = "Inventar.Admin";
}

var protectKeysWithDpapi = builder.Configuration.GetValue<bool>("DataProtection:ProtectKeysWithDpapi");

Directory.CreateDirectory(dataProtectionKeysPath);

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName(dataProtectionApplicationName)
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

if (OperatingSystem.IsWindows() && protectKeysWithDpapi)
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        inventarConnectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sqlOptions.CommandTimeout(30);
        });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddRoles<IdentityRole>()
    .AddDefaultTokenProviders();

builder.Services.AddMemoryCache();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "Inventar.Admin.AntiForgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "Inventar.Admin.Session";
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = LocalizationSettings.SupportedCultures.ToList();

    options.DefaultRequestCulture = new RequestCulture(LocalizationSettings.DefaultCultureName);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders =
    [
        new CustomRequestCultureProvider(context =>
        {
            if (context.Request.Cookies.TryGetValue(CookieRequestCultureProvider.DefaultCookieName, out var requestCultureCookie))
            {
                var cookieCultureName = LocalizationSettings.TryExtractSupportedCultureFromCookie(requestCultureCookie);
                if (!string.IsNullOrWhiteSpace(cookieCultureName))
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(cookieCultureName, cookieCultureName));
                }
            }

            if (context.Request.Cookies.TryGetValue("Language", out var legacyLanguageCookie))
            {
                var legacyCultureName = LocalizationSettings.TryExtractSupportedCultureFromCookie(legacyLanguageCookie);
                if (!string.IsNullOrWhiteSpace(legacyCultureName))
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(legacyCultureName, legacyCultureName));
                }
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        })
    ];
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/Index";
    options.AccessDeniedPath = "/Home/Error";
    options.SlidingExpiration = true;
    options.Cookie.Name = "Inventar.Admin.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.RequireHeaderSymmetry = false;

    foreach (var proxy in reverseProxySettings.KnownProxies)
    {
        if (IPAddress.TryParse(proxy, out var parsedProxy))
        {
            options.KnownProxies.Add(parsedProxy);
        }
    }

    foreach (var network in reverseProxySettings.KnownNetworks)
    {
        try
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
        catch (Exception)
        {
        }
    }
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
}

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."), tags: ["live"])
    .AddCheck<DatabaseConnectivityHealthCheck<ApplicationDbContext>>("database", tags: ["ready"]);

builder.Host.UseSerilog((context, services, configuration) =>
{
    var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
    var defaultLevel = context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Information : LogEventLevel.Warning;

    configuration
        .MinimumLevel.Is(defaultLevel)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With(new UserEnricher(httpContextAccessor))
        .Enrich.WithProperty("Application", "Inventar.Admin")
        .WriteTo.File(
            path: "Logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{UserName}] [{Application}] {Message:lj}{NewLine}{Exception}"
        );
});

var app = builder.Build();

if (args.Length == 1 && args[0].Equals("seeddata", StringComparison.OrdinalIgnoreCase))
{
    await Seed.SeedUsersAndRolesAsync(app);
}

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
if (!app.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(inventarConnectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:Inventar must be configured before starting in production.");
    }

    if (string.Equals(builder.Configuration["AllowedHosts"], "*", StringComparison.Ordinal))
    {
        startupLogger.LogWarning("AllowedHosts is set to '*'. Restrict it to the production host names before deployment.");
    }

    if (reverseProxySettings.Enabled &&
        reverseProxySettings.KnownProxies.Count == 0 &&
        reverseProxySettings.KnownNetworks.Count == 0)
    {
        startupLogger.LogWarning("ReverseProxy is enabled but no KnownProxies or KnownNetworks are configured.");
    }

    if (hostRedirectSettings.Enabled && !hostRedirectSettings.HasValidConfiguration())
    {
        startupLogger.LogWarning("HostRedirect is enabled but it is missing DestinationHost or SourceHosts.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.Use(async (context, next) =>
{
    if (hostRedirectSettings.ShouldRedirect(context.Request.Host.Host))
    {
        var redirectUri = new UriBuilder(Uri.UriSchemeHttps, hostRedirectSettings.DestinationHost)
        {
            Path = $"{context.Request.PathBase}{context.Request.Path}",
            Query = context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value![1..]
                : string.Empty
        };

        context.Response.StatusCode = hostRedirectSettings.Permanent
            ? StatusCodes.Status308PermanentRedirect
            : StatusCodes.Status307TemporaryRedirect;
        context.Response.Headers.Location = redirectUri.Uri.AbsoluteUri;
        return;
    }

    await next();
});
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!app.Environment.IsDevelopment())
        {
            context.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=86400";
        }
    }
});
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        if (!string.IsNullOrWhiteSpace(context.Response.ContentType)
            && context.Response.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
            && !context.Response.ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
        }

        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("Permissions-Policy", "camera=(self), geolocation=(), microphone=(), payment=()");
        context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
        context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
        context.Response.Headers.TryAdd(
            "Content-Security-Policy",
            "base-uri 'self'; form-action 'self'; frame-ancestors 'self'; object-src 'none'");

        return Task.CompletedTask;
    });

    await next();
});
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseSession();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
