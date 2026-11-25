using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelPlanner.Api.DTOs;
using TravelPlanner.Api.Logging;
using TravelPlanner.Api.Services;

namespace TravelPlanner.Api.Controllers;

[ApiController]
[Route("api/travel")]
[Tags("Travel providers")]
[EnableRateLimiting("travel-providers")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
public sealed partial class TravelController(TravelProviderService provider) : ControllerBase
{
    [HttpGet("hotels", Name = ApiLogNames.HotelsReqResp)]
    [EndpointSummary("Find hotel listing pages by destination")]
    [ProducesResponseType(typeof(HotelSearchResponse), StatusCodes.Status200OK)]
    public Task<IResult> Hotels([FromQuery] HotelSearchRequest request, CancellationToken token)
    {
        var destination = request.Destination?.Trim();
        return destination is { Length: >= 2 and <= 100 }
            ? provider.HotelsAsync(destination, token)
            : Task.FromResult(Invalid("destination", "Enter a destination between 2 and 100 characters."));
    }

    [HttpGet("trains/{trainNumber}/fare", Name = ApiLogNames.TrainFareReqResp)]
    [EndpointSummary("Look up a fare for a known Indian Railways train")]
    [ProducesResponseType(typeof(TrainFareResponse), StatusCodes.Status200OK)]
    public async Task<IResult> TrainFare(string trainNumber, [FromQuery] TrainFareRequest request, CancellationToken token)
    {
        var source = Normalize(request.Source);
        var destination = Normalize(request.Destination);
        var travelClass = Normalize(request.TravelClass);
        var quota = Normalize(request.Quota ?? "GN");
        var date = request.Date;
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(330)).DateTime);

        if (!TrainNumber().IsMatch(trainNumber))
            return Invalid("trainNumber", "Enter a five-digit train number.");
        if (!StationCode().IsMatch(source))
            return Invalid("source", "Enter a station code such as DADN.");
        if (!StationCode().IsMatch(destination) || destination == source)
            return Invalid("destination", "Enter a different destination station code.");
        if (!ClassCode().IsMatch(travelClass))
            return Invalid("travelClass", "Enter a class code such as 3A or SL.");
        if (!QuotaCode().IsMatch(quota))
            return Invalid("quota", "Enter a two-letter quota code such as GN.");
        if (date is null || date < today.AddDays(-1) || date > today.AddDays(90))
            return Invalid("date", "Enter a journey date from yesterday through 90 days ahead in India.");

        return await provider.TrainFareAsync(trainNumber, source, destination, travelClass, quota,
            date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), token);
    }

    [HttpGet("flights", Name = ApiLogNames.FlightsReqResp)]
    [EndpointSummary("Look up current flights by flight code or airport pair")]
    [ProducesResponseType(typeof(FlightSearchResponse), StatusCodes.Status200OK)]
    public async Task<IResult> Flights([FromQuery] FlightSearchRequest request, CancellationToken token)
    {
        var (flightCode, origin, destination) = request;
        if (flightCode is not null)
        {
            flightCode = Normalize(flightCode);
            if (!FlightCode().IsMatch(flightCode) || origin is not null || destination is not null)
                return Invalid("flightCode", "Enter a flight code such as AI101, or provide an airport pair.");
            return await provider.FlightsAsync(new Dictionary<string, string?> { ["flight_iata"] = flightCode }, token);
        }

        origin = Normalize(origin);
        destination = Normalize(destination);
        if (!AirportCode().IsMatch(origin) || !AirportCode().IsMatch(destination) || origin == destination)
            return Invalid("origin", "Enter two different three-letter IATA airport codes.");

        return await provider.FlightsAsync(new Dictionary<string, string?>
        {
            ["dep_iata"] = origin,
            ["arr_iata"] = destination
        }, token);
    }

    [HttpPost("itinerary", Name = ApiLogNames.ItineraryReqResp)]
    [EndpointSummary("Generate a suggested itinerary with Groq")]
    [ProducesResponseType(typeof(ItineraryResponse), StatusCodes.Status200OK)]
    public async Task<IResult> Itinerary(ItineraryRequest request, CancellationToken token)
    {
        var destination = request.Destination?.Trim();
        var interests = request.Interests?.Trim();
        if (destination is not { Length: >= 2 and <= 100 })
            return Invalid("destination", "Enter a destination between 2 and 100 characters.");
        if (request.Days is < 1 or > 14)
            return Invalid("days", "Enter a trip length of 1 to 14 days.");
        if (interests is { Length: > 400 })
            return Invalid("interests", "Enter up to 400 characters.");

        return await provider.ItineraryAsync(new ItineraryRequest(destination, request.Days, interests), token);
    }

    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? "";

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex TrainNumber();
    [GeneratedRegex("^[A-Z0-9]{2,8}$")]
    private static partial Regex StationCode();
    [GeneratedRegex("^[A-Z0-9]{2,3}$")]
    private static partial Regex ClassCode();
    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex QuotaCode();
    [GeneratedRegex("^[A-Z0-9]{2}[0-9]{1,4}[A-Z]?$")]
    private static partial Regex FlightCode();
    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex AirportCode();
}

