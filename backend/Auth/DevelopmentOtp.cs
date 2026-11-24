using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace TravelPlanner.Api.Auth;

internal sealed partial class DevelopmentOtp(IMemoryCache cache)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 5;
    private readonly object gate = new();

    public IResult Request(OtpRequest request)
    {
        var match = IndianMobileNumber().Match(request.PhoneNumber?.Trim() ?? "");
        if (!match.Success)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["phoneNumber"] = ["Enter a 10-digit Indian mobile number, optionally prefixed with +91."]
            });

        var phone = "+91" + match.Groups[1].Value;
        lock (gate)
        {
            if (cache.TryGetValue((phone, "cooldown"), out _))
            {
                return Results.Problem(
                    detail: "Wait 30 seconds between code requests for this number.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (cache.TryGetValue<Guid>((phone, "challenge"), out var previousId))
                cache.Remove(previousId);

            var challengeId = Guid.NewGuid();
            var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            cache.Set(challengeId, new PendingChallenge(phone, code), CodeLifetime);
            cache.Set((phone, "challenge"), challengeId, CodeLifetime);
            cache.Set((phone, "cooldown"), true, ResendDelay);

            return Results.Ok(new OtpChallengeResponse(challengeId, (int)CodeLifetime.TotalSeconds, code));
        }
    }

    public IResult Verify(OtpVerification request)
    {
        lock (gate)
        {
            if (!cache.TryGetValue<PendingChallenge>(request.ChallengeId, out var challenge)
                || challenge is null)
                return InvalidCode();

            if (!string.Equals(request.Code, challenge.Code, StringComparison.Ordinal))
            {
                challenge.FailedAttempts++;
                if (challenge.FailedAttempts >= MaxAttempts)
                    cache.Remove(request.ChallengeId);
                return InvalidCode();
            }

            cache.Remove(request.ChallengeId);
            cache.Remove((challenge.Phone, "challenge"));
            return Results.Ok(new OtpVerificationResponse("signup_required"));
        }
    }

    private static IResult InvalidCode() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["code"] = ["The code is invalid or expired. Request a new code if needed."]
    });

    [GeneratedRegex(@"^(?:\+91\s*)?([6-9][0-9]{9})$")]
    private static partial Regex IndianMobileNumber();

    private sealed record PendingChallenge(string Phone, string Code)
    {
        public int FailedAttempts { get; set; }
    }
}

internal sealed record OtpRequest(string? PhoneNumber);
internal sealed record OtpChallengeResponse(Guid ChallengeId, int ExpiresInSeconds, string DevelopmentCode);
internal sealed record OtpVerification(Guid ChallengeId, string? Code);
internal sealed record OtpVerificationResponse(string Status);
