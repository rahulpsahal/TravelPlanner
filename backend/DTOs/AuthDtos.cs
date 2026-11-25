namespace TravelPlanner.Api.DTOs;

public sealed record OtpRequest(string? PhoneNumber);
public sealed record OtpChallengeResponse(Guid ChallengeId, int ExpiresInSeconds, string DevelopmentCode);
public sealed record OtpVerification(Guid ChallengeId, string? Code);
public sealed record OtpVerificationResponse(string Status);

