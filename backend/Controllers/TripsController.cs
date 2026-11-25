using Microsoft.AspNetCore.Mvc;
using TravelPlanner.Api.DTOs;
using TravelPlanner.Api.Logging;
using TravelPlanner.Api.Services;

namespace TravelPlanner.Api.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController(TripPreviewService preview) : ControllerBase
{
    [HttpPost("preview", Name = ApiLogNames.TripPreviewReqResp)]
    [EndpointSummary("Compare sample Delhi to Goa trip options")]
    [EndpointDescription("Use future dates in YYYY-MM-DD format and a total budget in INR.")]
    [ProducesResponseType(typeof(TripPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IResult Preview(TripPreviewRequest request) => preview.Create(request);
}
