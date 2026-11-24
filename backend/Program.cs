using Microsoft.AspNetCore.RateLimiting;
using TravelPlanner.Api.Auth;
using TravelPlanner.Api.Trips;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<DevelopmentOtp>();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("development-otp", limiter =>
        {
            limiter.PermitLimit = 60;
            limiter.Window = TimeSpan.FromMinutes(1);
        });
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseRouting();
    app.UseRateLimiter();

    var auth = app.MapGroup("/api/auth/otp")
        .WithTags("Development OTP")
        .RequireRateLimiting("development-otp");
    auth.MapPost("/request", (OtpRequest request, DevelopmentOtp otp) => otp.Request(request))
        .Produces<OtpChallengeResponse>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status429TooManyRequests);
    auth.MapPost("/verify", (OtpVerification request, DevelopmentOtp otp) => otp.Verify(request))
        .Produces<OtpVerificationResponse>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status429TooManyRequests);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/trips/preview", TripPreview.Create)
    .WithSummary("Compare sample Delhi to Goa trip options")
    .WithDescription("Use future dates in YYYY-MM-DD format and a total budget in INR.")
    .ProducesValidationProblem();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
