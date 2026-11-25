using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelPlanner.Api.Controllers;
using TravelPlanner.Api.Data;
using TravelPlanner.Api.Logging;
using TravelPlanner.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var controllers = builder.Services.AddControllers();
if (!builder.Environment.IsDevelopment())
    controllers.ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new ExcludeDevelopmentOtp()));

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
    options.AddFixedWindowLimiter("development-otp", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var connectionString = builder.Configuration.GetConnectionString("TravelPlanner");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContextFactory<TravelPlannerDbContext>(options =>
        options.UseSqlServer(connectionString));
}

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevelopmentOtpService>();
}

var app = builder.Build();
app.UseRouting();
app.UseMiddleware<ApiRequestResponseLogging>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
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
