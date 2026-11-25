using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TravelPlanner.Api.Data;

namespace TravelPlanner.Api.Logging;

internal sealed class ApiRequestResponseLogging(
    RequestDelegate next, ILogger<ApiRequestResponseLogging> logger)
{
    private const string RequestIdHeader = "X-Request-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var methodName = endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? ApiLogNames.ApiReqResp;
        var routeTemplate = (endpoint as RouteEndpoint)?.RoutePattern.RawText;
        var route = routeTemplate is null ? "(unmatched)" : "/" + routeTemplate.TrimStart('/');
        var method = context.Request.Method;
        var traceId = context.TraceIdentifier;
        var requestId = Guid.NewGuid();
        context.Response.Headers[RequestIdHeader] = requestId.ToString("D");
        var start = Stopwatch.GetTimestamp();
        var createdAt = DateTimeOffset.UtcNow;
        var databaseFactory = context.RequestServices.GetService<IDbContextFactory<TravelPlannerDbContext>>();
        var capturePayload = databaseFactory is not null
            && !context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase);
        var captureRequest = capturePayload && ApiLogCapture.IsJson(context.Request.ContentType)
            && context.Request.ContentLength is > 0 and <= ApiLogCapture.BodyLimit;
        if (captureRequest)
            context.Request.EnableBuffering();

        var originalBody = context.Response.Body;
        using var capture = capturePayload ? new LimitedResponseCapture(originalBody) : null;
        if (capture is not null)
            context.Response.Body = capture;

        logger.LogInformation("{MethodName} request {HttpMethod} {Route} request ID {RequestId} trace {TraceId}",
            methodName, method, route, requestId, traceId);

        var statusCode = StatusCodes.Status500InternalServerError;
        string? errorType = null;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        catch (Exception exception)
        {
            var canceled = exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested;
            statusCode = context.Response.HasStarted ? context.Response.StatusCode
                : canceled ? StatusCodes.Status499ClientClosedRequest : StatusCodes.Status500InternalServerError;
            errorType = exception.GetType().Name;
            throw;
        }
        finally
        {
            if (capture is not null)
                context.Response.Body = originalBody;

            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            logger.Log(errorType is null ? LogLevel.Information : LogLevel.Warning,
                "{MethodName} response {HttpMethod} {Route} status {StatusCode} in {ElapsedMs:F2} ms request ID {RequestId} trace {TraceId} error {ErrorType}",
                methodName, method, route, statusCode, elapsedMs, requestId, traceId, errorType);

            if (databaseFactory is not null)
            {
                await SaveAsync(databaseFactory, context, new ApiLog
                {
                    RequestId = requestId, MethodName = methodName, TraceId = traceId, CreatedAt = createdAt,
                    StatusCode = statusCode, ElapsedMs = elapsedMs, RequestJson = "{}", ResponseJson = "{}"
                }, route, captureRequest, capture, errorType);
            }
        }
    }

    private async Task SaveAsync(IDbContextFactory<TravelPlannerDbContext> databaseFactory, HttpContext context,
        ApiLog log, string route, bool captureRequest, LimitedResponseCapture? capture, string? errorType)
    {
        try
        {
            log.RequestJson = JsonSerializer.Serialize(new
            {
                method = context.Request.Method,
                route,
                query = capture is null ? null : ApiLogCapture.ReadQuery(context.Request.Query),
                body = captureRequest ? await ApiLogCapture.ReadRequestAsync(context.Request) : null
            });
            log.ResponseJson = JsonSerializer.Serialize(new
            {
                statusCode = log.StatusCode,
                body = ApiLogCapture.IsJson(context.Response.ContentType) ? capture?.ReadJson() : null,
                errorType
            });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var database = await databaseFactory.CreateDbContextAsync(timeout.Token);
            database.ApiLogs.Add(log);
            await database.SaveChangesAsync(timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Could not store {MethodName} API log for trace {TraceId}: {ErrorType}",
                log.MethodName, log.TraceId, exception.GetType().Name);
        }
    }
}
