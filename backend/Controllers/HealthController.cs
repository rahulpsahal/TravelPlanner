using Microsoft.AspNetCore.Mvc;
using TravelPlanner.Api.DTOs;
using TravelPlanner.Api.Logging;

namespace TravelPlanner.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet(Name = ApiLogNames.HealthReqResp)]
    public HealthResponse Get() => new("ok");
}
