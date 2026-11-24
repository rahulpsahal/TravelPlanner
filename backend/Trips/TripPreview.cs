namespace TravelPlanner.Api.Trips;

internal static class TripPreview
{
    private static readonly Flight[] Flights =
    [
        new("Sample Saver Flight", 7_200m),
        new("Sample Flexible Flight", 9_000m)
    ];

    private static readonly Hotel[] Hotels =
    [
        new("Sample Budget Hotel", 2_400m),
        new("Sample Comfort Hotel", 3_500m)
    ];

    public static IResult Create(TripPreviewRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var todayInIndia = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(
            TimeSpan.FromMinutes(330)).DateTime);

        if (!string.Equals(request.Origin?.Trim(), "Delhi", StringComparison.OrdinalIgnoreCase))
            errors["origin"] = ["The demo supports Delhi as the origin."];

        if (!string.Equals(request.Destination?.Trim(), "Goa", StringComparison.OrdinalIgnoreCase))
            errors["destination"] = ["The demo supports Goa as the destination."];

        if (request.DepartureDate is null || request.DepartureDate < todayInIndia)
            errors["departureDate"] = ["Enter a departure date of today or later."];

        if (request.ReturnDate is null || request.ReturnDate <= request.DepartureDate)
            errors["returnDate"] = ["Enter a return date after the departure date."];

        if (request.Travelers < 1)
            errors["travelers"] = ["Enter at least one traveler."];

        if (request.Budget <= 0)
            errors["budget"] = ["Enter a budget greater than zero in INR."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var nights = request.ReturnDate!.Value.DayNumber - request.DepartureDate!.Value.DayNumber;
        const int guestsPerRoom = 2;
        var rooms = (int)Math.Ceiling(request.Travelers / (decimal)guestsPerRoom);

        var options = (
            from flight in Flights
            from hotel in Hotels
            let transportTotal = flight.RoundTripPerTraveler * request.Travelers
            let lodgingTotal = hotel.NightlyRatePerRoom * nights * rooms
            let total = transportTotal + lodgingTotal
            orderby total
            select new TripOption(flight, hotel, transportTotal, lodgingTotal, total, request.Budget - total)
        ).ToArray();

        var withinBudget = options.Where(option => option.BudgetDifference >= 0).ToArray();

        return Results.Ok(new
        {
            dataSource = "demo",
            currency = "INR",
            origin = "Delhi",
            destination = "Goa",
            request.DepartureDate,
            request.ReturnDate,
            request.Travelers,
            request.Budget,
            nights,
            rooms,
            hasWithinBudgetOptions = withinBudget.Length > 0,
            options = withinBudget.Length > 0 ? withinBudget : [options[0]]
        });
    }

    private sealed record Flight(string Name, decimal RoundTripPerTraveler)
    {
        public string ProviderSearchUrl => "https://www.google.com/travel/flights";
    }

    private sealed record Hotel(string Name, decimal NightlyRatePerRoom)
    {
        public string ProviderSearchUrl => "https://www.google.com/travel/hotels";
    }

    private sealed record TripOption(
        Flight Flight, Hotel Hotel, decimal TransportTotal, decimal LodgingTotal,
        decimal Total, decimal BudgetDifference);
}

internal sealed record TripPreviewRequest(
    string? Origin,
    string? Destination,
    DateOnly? DepartureDate,
    DateOnly? ReturnDate,
    int Travelers,
    decimal Budget);
