using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Inventar.Storefront.Data;
using Inventar.Storefront.Services;
using Inventar.Storefront.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
var storefrontConnectionString = builder.Configuration.GetConnectionString("Inventar");

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddDbContext<StorefrontDbContext>(options =>
{
    options.UseSqlServer(
        storefrontConnectionString,
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
builder.Services.AddOptions<StorefrontSettings>()
    .BindConfiguration(StorefrontSettings.SectionName)
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.BrandName), "Storefront:BrandName must be configured.")
    .Validate(settings => settings.ReservationHours > 0, "Storefront:ReservationHours must be greater than zero.")
    .Validate(settings => settings.FlatShippingCost >= 0, "Storefront:FlatShippingCost can not be negative.")
    .Validate(settings => settings.RememberCustomerForDays > 0, "Storefront:RememberCustomerForDays must be greater than zero.")
    .Validate(settings => settings.MaxLoginCodeAttempts > 0, "Storefront:MaxLoginCodeAttempts must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddOptions<StorefrontEmailSettings>()
    .BindConfiguration(StorefrontEmailSettings.SectionName)
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.SenderEmail), "StorefrontEmail:SenderEmail must be configured.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.SenderDisplayName), "StorefrontEmail:SenderDisplayName must be configured.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.SmtpHost), "StorefrontEmail:SmtpHost must be configured.")
    .Validate(settings => settings.SmtpPort > 0, "StorefrontEmail:SmtpPort must be greater than zero.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.SmtpUsername), "StorefrontEmail:SmtpUsername must be configured.")
    .Validate(settings => !StorefrontEmailSettings.UsesPlaceholder(settings.SenderEmail), "StorefrontEmail:SenderEmail still contains a placeholder value.")
    .Validate(settings => !StorefrontEmailSettings.UsesPlaceholder(settings.SmtpHost), "StorefrontEmail:SmtpHost still contains a placeholder value.")
    .Validate(settings => !StorefrontEmailSettings.UsesPlaceholder(settings.SmtpUsername), "StorefrontEmail:SmtpUsername still contains a placeholder value.")
    .Validate(settings => settings.VerificationCodeLifetimeMinutes > 0, "StorefrontEmail:VerificationCodeLifetimeMinutes must be greater than zero.")
    .Validate(
        settings => !builder.Environment.IsProduction() || !string.IsNullOrWhiteSpace(settings.SmtpPassword),
        "StorefrontEmail:SmtpPassword must be configured in production.")
    .ValidateOnStart();
builder.Services.AddOptions<StorefrontGoogleAuthSettings>()
    .BindConfiguration(StorefrontGoogleAuthSettings.SectionName)
    .Validate(
        settings => string.IsNullOrWhiteSpace(settings.CallbackPath) || settings.CallbackPath.StartsWith('/'),
        "StorefrontGoogleAuth:CallbackPath must start with '/'.")
    .ValidateOnStart();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "Inventar.Storefront.AntiForgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
});

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
    dataProtectionApplicationName = "Inventar.Storefront";
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

var googleAuthSettings = builder.Configuration
    .GetSection(StorefrontGoogleAuthSettings.SectionName)
    .Get<StorefrontGoogleAuthSettings>() ?? new StorefrontGoogleAuthSettings();

var formActionSources = new List<string> { "'self'" };
if (googleAuthSettings.IsConfigured)
{
    formActionSources.Add("https://accounts.google.com");
}

var authenticationBuilder = builder.Services.AddAuthentication(StorefrontAuthenticationConstants.AuthenticationScheme)
    .AddCookie(StorefrontAuthenticationConstants.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "Inventar.Storefront.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = secureCookiePolicy;
        options.LoginPath = "/nalog/prijava";
        options.AccessDeniedPath = "/nalog/prijava";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    })
    .AddCookie(StorefrontAuthenticationConstants.ExternalAuthenticationScheme, options =>
    {
        options.Cookie.Name = "Inventar.Storefront.ExternalAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = secureCookiePolicy;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

if (googleAuthSettings.IsConfigured)
{
    authenticationBuilder.AddOAuth(StorefrontAuthenticationConstants.GoogleAuthenticationScheme, options =>
    {
        options.SignInScheme = StorefrontAuthenticationConstants.ExternalAuthenticationScheme;
        options.ClientId = googleAuthSettings.ClientId;
        options.ClientSecret = googleAuthSettings.ClientSecret;
        options.CallbackPath = googleAuthSettings.CallbackPath;
        options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        options.TokenEndpoint = "https://oauth2.googleapis.com/token";
        options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
        options.SaveTokens = false;
        options.UsePkce = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
        options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
        options.ClaimActions.MapJsonKey("urn:google:email_verified", "email_verified");
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.IsEssential = true;
        options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = secureCookiePolicy;
        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                using var response = await context.Backchannel.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.HttpContext.RequestAborted);

                response.EnsureSuccessStatusCode();

                await using var responseStream = await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted);
                using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: context.HttpContext.RequestAborted);
                context.RunClaimActions(payload.RootElement);
            }
        };
        options.AuthorizationEndpoint += "?prompt=select_account";
    });
}

builder.Services.AddAuthorization();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "Inventar.Storefront.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
    options.IdleTimeout = TimeSpan.FromHours(6);
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
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("Application is running."), tags: ["live"])
    .AddCheck<DatabaseConnectivityHealthCheck<StorefrontDbContext>>("database", tags: ["ready"]);
builder.Services.AddScoped<ICartService, SessionCartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICategoryNavigationService, CategoryNavigationService>();
builder.Services.AddScoped<StorefrontPoMjeriInventoryService>();
builder.Services.AddScoped<IPendingCheckoutStore, SessionPendingCheckoutStore>();
builder.Services.AddScoped<IPendingAccountLoginStore, SessionPendingAccountLoginStore>();
builder.Services.AddScoped<IStorefrontEmailService, SmtpStorefrontEmailService>();
builder.Services.AddScoped<IStorefrontCustomerService, StorefrontCustomerService>();

builder.Host.UseSerilog((context, services, configuration) =>
{
    var defaultLevel = context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Information : LogEventLevel.Warning;

    configuration
        .MinimumLevel.Is(defaultLevel)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Inventar.Storefront")
        .WriteTo.File(
            path: "Logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Application}] {Message:lj}{NewLine}{Exception}"
        );
});

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

if (StorefrontGoogleAuthSettings.UsesPlaceholder(googleAuthSettings.ClientId) ||
    StorefrontGoogleAuthSettings.UsesPlaceholder(googleAuthSettings.ClientSecret))
{
    startupLogger.LogWarning(
        "Storefront Google login is disabled because StorefrontGoogleAuth contains placeholder credentials. Configure a real Google OAuth web client before enabling this login option.");
}

if (!app.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(storefrontConnectionString))
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

    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseResponseCompression();
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
        context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=(), payment=()");
        context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
        context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", "same-origin");
        context.Response.Headers.TryAdd(
            "Content-Security-Policy",
            $"base-uri 'self'; form-action {string.Join(' ', formActionSources.Distinct(StringComparer.OrdinalIgnoreCase))}; frame-ancestors 'self'; object-src 'none'");

        return Task.CompletedTask;
    });

    await next();
});
app.UseRouting();
app.UseSession();
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
