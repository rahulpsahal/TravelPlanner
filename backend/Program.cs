using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelPlanner.Api.Controllers;
using TravelPlanner.Api.Data;
using TravelPlanner.Api.Logging;
using TravelPlanner.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var development = builder.Environment.IsDevelopment();
var fixedOtp = FixedOtpSettings.Read(builder.Configuration);
var authEnabled = development || fixedOtp.Enabled;
var controllers = builder.Services.AddControllers();
if (!authEnabled)
    controllers.ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new ExcludeAuthController()));

builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<TripPreviewService>();
builder.Services.AddHttpClient<TravelProviderService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
})
    // Aviationstack puts the API key in the query string.
    .RemoveAllLoggers()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("travel-providers", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("authentication", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var connectionString = builder.Configuration.GetConnectionString("TravelPlanner");
if (fixedOtp.Enabled && string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Fixed OTP requires the TravelPlanner SQL Server connection string.");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContextFactory<TravelPlannerDbContext>(options =>
        options.UseSqlServer(connectionString));
}

if (authEnabled)
{
    builder.Services.AddSingleton(fixedOtp);
    builder.Services.AddSingleton<OtpService>();
    builder.Services.AddScoped<AccountService>();
    builder.Services.AddAuthentication(AuthSchemes.Session)
        .AddCookie(AuthSchemes.Session, options =>
        {
            ConfigureAuthCookie(options, ".TravelPlanner.Session", "/api", TimeSpan.FromHours(8), development);
        })
        .AddCookie(AuthSchemes.VerifiedSignup, options =>
        {
            ConfigureAuthCookie(options, ".TravelPlanner.Signup", "/api/auth", TimeSpan.FromMinutes(10), development);
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();
app.UseRouting();
app.UseMiddleware<ApiRequestResponseLogging>();
app.UseRateLimiter();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (development)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound())
    .WithName(ApiLogNames.ApiNotFoundReqResp);
app.MapFallbackToFile("index.html");

app.Run();

static void ConfigureAuthCookie(CookieAuthenticationOptions options, string name, string path, TimeSpan lifetime,
    bool development)
{
    options.Cookie.Name = name;
    options.Cookie.Path = path;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = lifetime;
    options.SlidingExpiration = false;
}
