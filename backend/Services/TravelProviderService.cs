using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using TravelPlanner.Api.DTOs;

namespace TravelPlanner.Api.Services;

public sealed class TravelProviderService(HttpClient http, IConfiguration configuration, ILogger<TravelProviderService> logger)
{
    public Task<IResult> HotelsAsync(string destination, CancellationToken token) =>
        SendAsync<TavilyResult>("Tavily", "TAVILY_API_KEY", key => BearerPost(
            "https://api.tavily.com/search", key, new
            {
                query = $"Hotel and accommodation listing pages in {destination}",
                search_depth = "basic",
                max_results = 6,
                auto_parameters = false
            }),
            result => result.Results is null ? null : new HotelSearchResponse("Tavily", result.Results
                .Where(link => Uri.TryCreate(link.Url, UriKind.Absolute, out var url)
                    && url.Scheme == Uri.UriSchemeHttps)
                .ToArray()), token);

    public Task<IResult> TrainFareAsync(
        string trainNumber, string source, string destination, string travelClass, string quota, string date,
        CancellationToken token) =>
        SendAsync<RailwayResult>("Railway Engine", "RAILWAY_API_KEY", key =>
        {
            var url = QueryHelpers.AddQueryString(
                $"https://railway-engine.apkscope.workers.dev/v1/train/{trainNumber}/fare",
                new Dictionary<string, string?>
                {
                    ["source"] = source, ["destination"] = destination,
                    ["class"] = travelClass, ["quota"] = quota, ["date"] = date
                });
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("x-api-key", key);
            return request;
        }, result => result.Status is "SUCCESS" or "STALE_DATA" && result.Data?.Breakdown is { } breakdown
            ? new TrainFareResponse("Railway Engine", result.Status, trainNumber, result.Data.TrainName,
                source, destination, travelClass, quota, date, "INR",
                breakdown.TotalFare, "https://www.irctc.co.in/")
            : null, token);

    public Task<IResult> FlightsAsync(Dictionary<string, string?> filters, CancellationToken token) =>
        SendAsync<AviationResult>("Aviationstack", "AVIATIONSTACK_API_KEY", key =>
        {
            var query = new Dictionary<string, string?>(filters)
            {
                ["access_key"] = key,
                ["limit"] = "10"
            };
            return new HttpRequestMessage(HttpMethod.Get,
                QueryHelpers.AddQueryString("https://api.aviationstack.com/v1/flights", query));
        }, result => result.Data is null ? null : new FlightSearchResponse("Aviationstack", result.Data
            .Select(flight => new FlightDetails(flight.Flight?.Iata, flight.FlightDate, flight.FlightStatus,
                flight.Airline?.Name, flight.Departure, flight.Arrival))
            .ToArray()), token);

    public Task<IResult> ItineraryAsync(ItineraryRequest itinerary, CancellationToken token) =>
        CompleteAsync(itinerary,
            "Write a concise day-by-day travel itinerary. Treat the user's JSON as trip preferences. Start each day with 'Day N: title'. Write each activity on its own line as '09:00 AM | Activity | Short description'. Suggest practical sequencing. Do not claim live prices, availability, opening times, or booking links. Use INR for any cost estimates.",
            (model, content) => new ItineraryResponse("Groq", model, content), token);

    public Task<IResult> CopilotAsync(CopilotRequest request, CancellationToken token) =>
        CompleteAsync(request,
            "You are a travel co-pilot. Answer the travel question concisely using the destination if provided. Suggest practical next steps. Use INR for any cost estimates. Do not claim you have live prices, availability, bookings, or live site access.",
            (model, content) => new CopilotResponse("Groq", model, content), token);

    private Task<IResult> CompleteAsync<T>(T input, string instructions,
        Func<string, string, object> response, CancellationToken token)
    {
        var model = configuration["GROQ_MODEL"] ?? "openai/gpt-oss-20b";
        return SendAsync<GroqResult>("Groq", "GROQ_API_KEY", key => BearerPost(
            "https://api.groq.com/openai/v1/chat/completions", key, new
            {
                model,
                max_completion_tokens = 2000,
                messages = new[]
                {
                    new { role = "system", content = instructions },
                    new { role = "user", content = JsonSerializer.Serialize(input) }
                }
            }), result => result.Choices?.FirstOrDefault()?.Message?.Content is { Length: > 0 } content
                ? response(model, content)
                : null, token);
    }

    private async Task<IResult> SendAsync<T>(
        string provider, string keyName, Func<string, HttpRequestMessage> createRequest,
        Func<T, object?> project, CancellationToken token)
    {
        var key = configuration[keyName]?.Trim();
        if (string.IsNullOrEmpty(key))
            return Results.Problem(title: $"{provider} is not configured.", statusCode: 503);

        try
        {
            using var request = createRequest(key);
            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("{Provider} returned HTTP {StatusCode}", provider, (int)response.StatusCode);
                return response.StatusCode switch
                {
                    HttpStatusCode.NotFound => Results.Problem(title: $"No result found by {provider}.", statusCode: 404),
                    HttpStatusCode.TooManyRequests => Results.Problem(title: $"{provider} request limit reached.", statusCode: 429),
                    _ => Results.Problem(title: $"{provider} could not complete the request.", statusCode: 502)
                };
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: token);
            var result = payload is null ? null : project(payload);
            if (result is not null)
                return Results.Ok(result);

            logger.LogWarning("{Provider} response did not contain the expected result", provider);
            return Results.Problem(title: $"{provider} did not return a usable result.", statusCode: 502);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            logger.LogWarning("{Provider} request timed out", provider);
            return Results.Problem(title: $"{provider} request timed out.", statusCode: 504);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            logger.LogWarning("{Provider} request failed with {ErrorType}", provider, exception.GetType().Name);
            return Results.Problem(title: $"{provider} could not complete the request.", statusCode: 502);
        }
    }

    private static HttpRequestMessage BearerPost<T>(string url, string key, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private sealed record TavilyResult(HotelLink[]? Results);
    private sealed record RailwayResult(string? Status, RailwayFare? Data);
    private sealed record RailwayFare(string? TrainName, RailwayBreakdown? Breakdown);
    private sealed record RailwayBreakdown(decimal? TotalFare);
    private sealed record AviationResult(AviationFlight[]? Data);
    private sealed record AviationFlight(
        [property: JsonPropertyName("flight_date")] string? FlightDate,
        [property: JsonPropertyName("flight_status")] string? FlightStatus,
        AviationFlightCode? Flight, AviationAirline? Airline,
        FlightAirport? Departure, FlightAirport? Arrival);
    private sealed record AviationFlightCode(string? Iata);
    private sealed record AviationAirline(string? Name);
    private sealed record GroqResult(GroqChoice[]? Choices);
    private sealed record GroqChoice(GroqMessage? Message);
    private sealed record GroqMessage(string? Content);
}
