using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelPlanner.Api.DTOs;
using TravelPlanner.Api.Logging;
using TravelPlanner.Api.Services;

namespace TravelPlanner.Api.Controllers;

[ApiController]
[Route("api/auth/otp")]
[Tags("Development OTP")]
[EnableRateLimiting("development-otp")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class DevelopmentOtpController(DevelopmentOtpService otp) : ControllerBase
{
    [HttpPost("request", Name = ApiLogNames.OtpRequestReqResp)]
    [ProducesResponseType(typeof(OtpChallengeResponse), StatusCodes.Status200OK)]
    public Task<IResult> RequestOtp(OtpRequest request, CancellationToken cancellationToken) =>
        otp.RequestAsync(request, cancellationToken);

    [HttpPost("verify", Name = ApiLogNames.OtpVerifyReqResp)]
    [ProducesResponseType(typeof(OtpVerificationResponse), StatusCodes.Status200OK)]
    public Task<IResult> VerifyOtp(OtpVerification request, CancellationToken cancellationToken) =>
        otp.VerifyAsync(request, cancellationToken);
}
