namespace TravelPlanner.Api.Data;

internal sealed class ApiLog
{
    public long Id { get; set; }
    public Guid RequestId { get; set; }
    public required string MethodName { get; set; }
    public required string TraceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int StatusCode { get; set; }
    public double ElapsedMs { get; set; }
    public required string RequestJson { get; set; }
    public required string ResponseJson { get; set; }
}
