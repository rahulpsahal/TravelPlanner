using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TravelPlanner.Api.Data;
using TravelPlanner.Api.DTOs;

namespace TravelPlanner.Api.Services;

public sealed partial class OtpService(
    IDbContextFactory<TravelPlannerDbContext> databaseFactory,
    FixedOtpSettings fixedOtp,
    ILogger<OtpService> logger)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ResendDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FixedOtpLockout = TimeSpan.FromHours(24);
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
        if (fixedOtp.Enabled && phone != fixedOtp.Phone)
            return Results.Ok(new OtpChallengeResponse(Guid.NewGuid(), (int)CodeLifetime.TotalSeconds, null));

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
                    detail: challenge.FailedAttempts >= MaxAttempts && fixedOtp.Enabled
                        ? "Too many incorrect codes. Try again after the lockout ends."
                        : "Wait 30 seconds between code requests for this number.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            if (challenge is null)
            {
                challenge = new OtpChallenge { PhoneNumber = phone };
                database.OtpChallenges.Add(challenge);
            }

            var code = fixedOtp.Code ?? RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            challenge.Id = Guid.NewGuid();
            challenge.CodeHash = HashCode(challenge.Id, code);
            challenge.ExpiresAt = now + CodeLifetime;
            challenge.ResendAvailableAt = now + ResendDelay;
            if (!fixedOtp.Enabled || challenge.FailedAttempts >= MaxAttempts)
                challenge.FailedAttempts = 0;
            await database.SaveChangesAsync(cancellationToken);

            logger.LogInformation("OTP challenge issued");
            return Results.Ok(new OtpChallengeResponse(challenge.Id, (int)CodeLifetime.TotalSeconds,
                fixedOtp.Enabled ? null : code));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string?> VerifyAsync(OtpVerification request, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
            var challenge = await database.OtpChallenges.SingleOrDefaultAsync(
                item => item.Id == request.ChallengeId, cancellationToken);

            if (challenge?.CodeHash is null || challenge.ExpiresAt <= DateTimeOffset.UtcNow
                || (fixedOtp.Enabled && challenge.PhoneNumber != fixedOtp.Phone))
            {
                logger.LogInformation("OTP verification rejected for an unavailable challenge");
                return null;
            }

            if (request.Code is not { Length: 6 } ||
                !CryptographicOperations.FixedTimeEquals(challenge.CodeHash, HashCode(challenge.Id, request.Code)))
            {
                challenge.FailedAttempts++;
                if (challenge.FailedAttempts >= MaxAttempts)
                {
                    challenge.CodeHash = null;
                    if (fixedOtp.Enabled)
                        challenge.ResendAvailableAt = DateTimeOffset.UtcNow + FixedOtpLockout;
                }

                await database.SaveChangesAsync(cancellationToken);
                logger.LogInformation("OTP verification rejected. Failed attempts: {FailedAttempts}", challenge.FailedAttempts);
                return null;
            }

            challenge.CodeHash = null;
            challenge.FailedAttempts = 0;
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("OTP verification succeeded");
            return challenge.PhoneNumber;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("OTP verification rejected because the challenge changed");
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private byte[] HashCode(Guid id, string code) =>
        HMACSHA256.HashData(codeKey, Encoding.UTF8.GetBytes($"{id:N}:{code}"));

    [GeneratedRegex(@"^(?:\+91\s*)?([6-9][0-9]{9})$")]
    private static partial Regex IndianMobileNumber();
}
