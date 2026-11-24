using TravelPlanner.Api.Trips;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
