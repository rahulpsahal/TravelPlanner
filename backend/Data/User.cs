namespace TravelPlanner.Api.Data;

internal sealed class User
{
    public Guid Id { get; set; }
    public required string PhoneNumber { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
