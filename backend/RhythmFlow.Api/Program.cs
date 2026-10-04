using Scalar.AspNetCore;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;
// Private, git-ignored overrides for this machine (PayFast keys, Gmail login, admin alert emails).
cfg.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// ---- Database: SQLite for local development, PostgreSQL for deployment (same code, config switch) ----
builder.Services.AddDbContext<AppDbContext>(o =>
{
    var cs = cfg.GetConnectionString("Default") ?? "Data Source=rhythmflow.db";
    if ((cfg["Database:Provider"] ?? "Sqlite").Equals("Postgres", StringComparison.OrdinalIgnoreCase)) o.UseNpgsql(NormalizePostgres(cs));
    else o.UseSqlite(cs);
});

// ---- Options ----
builder.Services.Configure<JwtOptions>(cfg.GetSection("Jwt"));
builder.Services.Configure<MediaOptions>(cfg.GetSection("Media"));
builder.Services.Configure<PayFastOptions>(cfg.GetSection("PayFast"));
builder.Services.Configure<BookingOptions>(cfg.GetSection("Booking"));
builder.Services.Configure<SmtpOptions>(cfg.GetSection("Smtp"));
builder.Services.Configure<AdminOptions>(cfg.GetSection("Admin"));

var jwt = cfg.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key must be at least 32 characters. Set it via configuration or an environment variable, never in source control.");
if ((cfg["Media:SigningKey"] ?? "").Length < 32) throw new InvalidOperationException("Media:SigningKey must be at least 32 characters.");

// ---- Services ----
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EntitlementService>();
builder.Services.AddScoped<ProgressService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddSingleton<MediaTokenService>();
builder.Services.AddSingleton<PayFastService>();
builder.Services.AddSingleton<IPayFastApi, PayFastApiClient>();
// Email goes out by SMTP (e.g. Gmail) by default, or through Brevo's web API with Email:Provider=Brevo
// (needed on hosts that block SMTP ports, such as Render's free plan).
builder.Services.Configure<BrevoOptions>(cfg.GetSection("Brevo"));
builder.Services.AddSingleton<IEmailSender>(sp =>
    string.Equals(cfg["Email:Provider"], "Brevo", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<BrevoEmailSender>(sp)
        : ActivatorUtilities.CreateInstance<EmailService>(sp));

// Interactive API documentation (OpenAPI document + Scalar page at /docs).
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Rhythm & Flow API";
    doc.Info.Version = "v1";
    doc.Info.Description = "REST API behind the Rhythm & Flow Android app: accounts, subscriptions (PayFast), protected workout videos, " +
                           "progress, classes and bookings, journal, notifications and admin tools. " +
                           "Log in with POST /api/auth/login, then paste the returned token into the Bearer authentication box.";
    doc.Components ??= new Microsoft.OpenApi.OpenApiComponents();
    doc.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
    doc.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "JWT returned by /api/auth/login or /api/auth/register",
    };
    return Task.CompletedTask;
}));
// Push notifications through Firebase Cloud Messaging. The service-account key comes from configuration
// (Firebase:ServiceAccountJson as a secret setting, or Firebase:ServiceAccountPath, or a firebase-service-account.json
// next to the app for local use). With no key, or a bad one, push is simply off and the app falls back to its periodic sync.
var firebaseCredential = TryLoadFirebaseCredential(cfg, builder.Environment.ContentRootPath);
if (firebaseCredential is not null)
{
    var firebaseApp = FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions { Credential = firebaseCredential });
    builder.Services.AddSingleton<IPushSender>(sp => new FirebasePushSender(
        FirebaseAdmin.Messaging.FirebaseMessaging.GetMessaging(firebaseApp), sp.GetRequiredService<ILogger<FirebasePushSender>>()));
}
else
{
    builder.Services.AddSingleton<IPushSender, NullPushSender>();
}
builder.Services.AddSingleton<ErrorLogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<ProgrammeAdminService>();
builder.Services.AddScoped<AdminOverviewService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("video", c => c.Timeout = TimeSpan.FromMinutes(30));
builder.Services.AddControllers();

// ---- Authentication / authorisation (JWT, roles) ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role,
    };
    // A token stops working when the account is disabled or its password changes (security stamp no longer matches).
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var sub = ctx.Principal?.FindFirstValue("sub");
            var stamp = ctx.Principal?.FindFirstValue("sv");
            var ok = Guid.TryParse(sub, out var id) && await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == id && u.AccountStatus == "ACTIVE" && u.SecurityStamp == stamp);
            if (!ok) ctx.Fail("Session is no longer valid.");
        },
    };
});
builder.Services.AddAuthorization();

// ---- Rate limiting on login/register to slow credential stuffing ----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = cfg.GetValue("RateLimit:AuthPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
    // Error reports come from the app and may be sent without a login, so they are limited per address.
    o.AddPolicy("telemetry", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

// Behind Azure's front door the API only sees plain HTTP from the proxy. Trust the proxy's headers so links the API
// builds (video playback, PayFast return URLs) use the real https address.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();
app.UseForwardedHeaders();

// ---- Create the schema and demo data on start. For production use EF Core migrations instead. ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SchemaUpgrades.ApplyAsync(db);   // newer tables for a database that was created by an earlier version
    await SeedData.RunAsync(db, cfg);
    // Placeholder timetable stays populated until the studio adds its own classes (set to false at launch to be sure).
    if (cfg.GetValue("Seed:KeepSampleClassesUpcoming", true)) await SeedData.EnsureUpcomingClassesAsync(db);
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapOpenApi();                               // /openapi/v1.json
app.MapScalarApiReference("/docs", o => o.WithTitle("Rhythm & Flow API"));  // interactive docs: /docs
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// Mark overdue subscriptions once an hour.
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SubscriptionService>().SweepExpiredAsync();
        }
        catch (Exception ex) { app.Logger.LogError(ex, "Subscription sweep failed"); }
        await Task.Delay(TimeSpan.FromHours(1));
    }
});

app.Run();

// Reads the Firebase service-account key from configuration; returns null (push off) when it is missing or unusable.
static Google.Apis.Auth.OAuth2.GoogleCredential? TryLoadFirebaseCredential(IConfiguration cfg, string contentRoot)
{
    try
    {
        var json = cfg["Firebase:ServiceAccountJson"];
        if (!string.IsNullOrWhiteSpace(json))
            return Google.Apis.Auth.OAuth2.CredentialFactory.FromJson<Google.Apis.Auth.OAuth2.ServiceAccountCredential>(json).ToGoogleCredential();

        var path = cfg["Firebase:ServiceAccountPath"];
        if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(contentRoot, "firebase-service-account.json");
        return File.Exists(path)
            ? Google.Apis.Auth.OAuth2.CredentialFactory.FromFile<Google.Apis.Auth.OAuth2.ServiceAccountCredential>(path).ToGoogleCredential()
            : null;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Push notifications are off: the Firebase key could not be read (" + ex.GetType().Name + ").");
        return null;
    }
}

// Hosts such as Render hand out the database as a URL (postgres://user:pass@host/db); Npgsql wants key=value pairs.
static string NormalizePostgres(string cs)
{
    if (!cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return cs;
    var uri = new Uri(cs);
    var parts = uri.UserInfo.Split(':', 2);
    var port = uri.Port > 0 ? uri.Port : 5432;
    return $"Host={uri.Host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={Uri.UnescapeDataString(parts[0])};Password={Uri.UnescapeDataString(parts.ElementAtOrDefault(1) ?? "")};" +
           "SSL Mode=Require;Trust Server Certificate=true";
}
