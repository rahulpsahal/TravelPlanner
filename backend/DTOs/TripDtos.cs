namespace TravelPlanner.Api.DTOs;

public sealed record TripFlight(string Name, decimal RoundTripPerTraveler)
{
    public string ProviderSearchUrl => "https://www.google.com/travel/flights";
}

public sealed record TripHotel(string Name, decimal NightlyRatePerRoom)
{
    public string ProviderSearchUrl => "https://www.google.com/travel/hotels";
}

public sealed record TripOption(
    TripFlight Flight, TripHotel Hotel, decimal TransportTotal, decimal LodgingTotal,
    decimal Total, decimal BudgetDifference);

public sealed record TripPreviewRequest(
    string? Origin,
    string? Destination,
    DateOnly? DepartureDate,
    DateOnly? ReturnDate,
    int Travelers,
    decimal Budget);

public sealed record TripPreviewResponse(
    string DataSource, string Currency, string Origin, string Destination,
    DateOnly DepartureDate, DateOnly ReturnDate, int Travelers, decimal Budget,
    int Nights, int Rooms, bool HasWithinBudgetOptions, TripOption[] Options);

