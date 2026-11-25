using System.Text.Json.Serialization;

namespace TravelPlanner.Api.DTOs;

public sealed record OtpRequest(string? PhoneNumber);
public sealed record OtpChallengeResponse(Guid ChallengeId, int ExpiresInSeconds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DevelopmentCode);
public sealed record OtpVerification(Guid ChallengeId, string? Code);
public sealed record OtpVerificationResponse(string Status);
public sealed record SignupRequest(string? DisplayName);
public sealed record CurrentUserResponse(Guid Id, string DisplayName, string PhoneNumber);

