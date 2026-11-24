namespace TravelPlanner.Api.Data;

internal sealed class OtpChallenge
{
    public required string PhoneNumber { get; set; }
    public Guid Id { get; set; }
    public byte[]? CodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ResendAvailableAt { get; set; }
    public int FailedAttempts { get; set; }
    public byte[] Version { get; set; } = [];
}
