using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelPlanner.Api.DTOs;
using TravelPlanner.Api.Logging;
using TravelPlanner.Api.Services;

namespace TravelPlanner.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Authentication")]
[EnableRateLimiting("authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class AuthController(OtpService otp, AccountService accounts) : ControllerBase
{
    [HttpPost("otp/request", Name = ApiLogNames.OtpRequestReqResp)]
    [ProducesResponseType(typeof(OtpChallengeResponse), StatusCodes.Status200OK)]
    public Task<IResult> RequestOtp(OtpRequest request, CancellationToken cancellationToken) =>
        otp.RequestAsync(request, cancellationToken);

    [HttpPost("otp/verify", Name = ApiLogNames.OtpVerifyReqResp)]
    [ProducesResponseType(typeof(OtpVerificationResponse), StatusCodes.Status200OK)]
    public async Task<IResult> VerifyOtp(OtpVerification request, CancellationToken cancellationToken)
    {
        var phone = await otp.VerifyAsync(request, cancellationToken);
        if (phone is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["code"] = ["The code is invalid or expired. Request a new code if needed."]
            });
        }

        var user = await accounts.FindByPhoneAsync(phone, cancellationToken);
        if (user is not null)
        {
            await StartSessionAsync(user);
            return Results.Ok(new OtpVerificationResponse("authenticated"));
        }

        await HttpContext.SignOutAsync(AuthSchemes.Session);
        await HttpContext.SignInAsync(AuthSchemes.VerifiedSignup,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.MobilePhone, phone)], AuthSchemes.VerifiedSignup)));
        return Results.Ok(new OtpVerificationResponse("signup_required"));
    }

    [HttpPost("signup", Name = ApiLogNames.SignupReqResp)]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Signup(SignupRequest request, CancellationToken cancellationToken)
    {
        var verification = await HttpContext.AuthenticateAsync(AuthSchemes.VerifiedSignup);
        var phone = verification.Principal?.FindFirstValue(ClaimTypes.MobilePhone);
        if (!verification.Succeeded || phone is null)
            return Results.Problem(title: "Verify your phone before signing up.", statusCode: 401);

        var displayName = request.DisplayName?.Trim();
        if (displayName is not { Length: >= 1 and <= 100 })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["displayName"] = ["Enter a display name between 1 and 100 characters."]
            });
        }

        var user = await accounts.RegisterAsync(phone, displayName, cancellationToken);
        if (user is null)
        {
            await HttpContext.SignOutAsync(AuthSchemes.VerifiedSignup);
            return Results.Problem(title: "This phone already has an account. Request a new code to sign in.", statusCode: 409);
        }

        await StartSessionAsync(user);
        return Results.Created("/api/auth/me", user);
    }

    [Authorize]
    [HttpGet("me", Name = ApiLogNames.AuthMeReqResp)]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IResult> Me(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            && await accounts.FindByIdAsync(id, cancellationToken) is { } user)
            return Results.Ok(user);

        await HttpContext.SignOutAsync(AuthSchemes.Session);
        return Results.Unauthorized();
    }

    [HttpPost("logout", Name = ApiLogNames.LogoutReqResp)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Session);
        await HttpContext.SignOutAsync(AuthSchemes.VerifiedSignup);
        return Results.NoContent();
    }

    private async Task StartSessionAsync(CurrentUserResponse user)
    {
        await HttpContext.SignOutAsync(AuthSchemes.VerifiedSignup);
        await HttpContext.SignInAsync(AuthSchemes.Session,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D"))], AuthSchemes.Session)));
    }
}
