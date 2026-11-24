using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TravelPlanner.Api.Data;

namespace TravelPlanner.Api.Auth;

internal sealed partial class DevelopmentOtp(
    IDbContextFactory<TravelPlannerDbContext> databaseFactory,
    ILogger<DevelopmentOtp> logger)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(30);
    private const int MaxAttempts = 5;
    private readonly SemaphoreSlim gate = new(1);
    private readonly byte[] codeKey = RandomNumberGenerator.GetBytes(32);

    public async Task<IResult> RequestAsync(OtpRequest request, CancellationToken cancellationToken)
    {
        var match = IndianMobileNumber().Match(request.PhoneNumber?.Trim() ?? "");
        if (!match.Success)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["phoneNumber"] = ["Enter a 10-digit Indian mobile number, optionally prefixed with +91."]
            });

        var phone = "+91" + match.Groups[1].Value;
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
            var challenge = await database.OtpChallenges.FindAsync([phone], cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (challenge is not null && challenge.ResendAvailableAt > now)
            {
                logger.LogInformation("OTP request throttled by resend cooldown");
                return Results.Problem(
                    detail: "Wait 30 seconds between code requests for this number.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (challenge is null)
            {
                challenge = new OtpChallenge { PhoneNumber = phone };
                database.OtpChallenges.Add(challenge);
            }

            var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            challenge.Id = Guid.NewGuid();
            challenge.CodeHash = HashCode(challenge.Id, code);
            challenge.ExpiresAt = now + CodeLifetime;
            challenge.ResendAvailableAt = now + ResendDelay;
            challenge.FailedAttempts = 0;
            await database.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Development OTP challenge issued");
            return Results.Ok(new OtpChallengeResponse(challenge.Id, (int)CodeLifetime.TotalSeconds, code));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IResult> VerifyAsync(OtpVerification request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
            var challenge = await database.OtpChallenges.SingleOrDefaultAsync(
                item => item.Id == request.ChallengeId, cancellationToken);

            if (challenge?.CodeHash is null || challenge.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                logger.LogInformation("OTP verification rejected for an unavailable challenge");
                return InvalidCode();
            }

            if (request.Code is not { Length: 6 } ||
                !CryptographicOperations.FixedTimeEquals(challenge.CodeHash, HashCode(challenge.Id, request.Code)))
            {
                challenge.FailedAttempts++;
                if (challenge.FailedAttempts >= MaxAttempts)
                    challenge.CodeHash = null;

                await database.SaveChangesAsync(cancellationToken);
                logger.LogInformation("OTP verification rejected. Failed attempts: {FailedAttempts}", challenge.FailedAttempts);
                return InvalidCode();
            }

            challenge.CodeHash = null;
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Development OTP verification succeeded");
            return Results.Ok(new OtpVerificationResponse("signup_required"));
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("OTP verification rejected because the challenge changed");
            return InvalidCode();
        }
        finally
        {
            gate.Release();
        }
    }

    private byte[] HashCode(Guid id, string code) =>
        HMACSHA256.HashData(codeKey, Encoding.UTF8.GetBytes($"{id:N}:{code}"));

    private static IResult InvalidCode() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["code"] = ["The code is invalid or expired. Request a new code if needed."]
    });

    [GeneratedRegex(@"^(?:\+91\s*)?([6-9][0-9]{9})$")]
    private static partial Regex IndianMobileNumber();
}

internal sealed record OtpRequest(string? PhoneNumber);
internal sealed record OtpChallengeResponse(Guid ChallengeId, int ExpiresInSeconds, string DevelopmentCode);
internal sealed record OtpVerification(Guid ChallengeId, string? Code);
internal sealed record OtpVerificationResponse(string Status);
