using Microsoft.AspNetCore.Mvc;

namespace TravelPlanner.Api.DTOs;

public sealed record HotelSearchRequest([FromQuery(Name = "destination")] string? Destination);
public sealed record TrainFareRequest(
    [FromQuery(Name = "source")] string? Source,
    [FromQuery(Name = "destination")] string? Destination,
    [FromQuery(Name = "travelClass")] string? TravelClass,
    [FromQuery(Name = "date")] DateOnly? Date,
    [FromQuery(Name = "quota")] string? Quota);
public sealed record FlightSearchRequest(
    [FromQuery(Name = "flightCode")] string? FlightCode,
    [FromQuery(Name = "origin")] string? Origin,
    [FromQuery(Name = "destination")] string? Destination);

public sealed record HotelSearchResponse(string Provider, HotelLink[] Results);
public sealed record HotelLink(string? Title, string? Url, string? Content);
public sealed record TrainFareResponse(
    string Provider, string Status, string TrainNumber, string? TrainName, string Source, string Destination,
    string TravelClass, string Quota, string Date, string Currency, decimal? TotalFare, string BookingSearchUrl);
public sealed record FlightSearchResponse(string Provider, FlightDetails[] Results);
public sealed record FlightDetails(
    string? FlightCode, string? FlightDate, string? Status, string? Airline,
    FlightAirport? Departure, FlightAirport? Arrival);
public sealed record FlightAirport(
    string? Airport, string? Iata, string? Scheduled, string? Estimated, string? Actual);
public sealed record ItineraryRequest(string? Destination, int Days, string? Interests);
public sealed record ItineraryResponse(string Provider, string Model, string Itinerary);
public sealed record CopilotRequest(string? Question, string? Destination);
public sealed record CopilotResponse(string Provider, string Model, string Answer);

