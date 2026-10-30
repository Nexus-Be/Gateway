namespace Gateway.Middlewares.Features;

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Gateway.Models;
using Microsoft.AspNetCore.Http;

public sealed class IdempotencyGuardMiddleware
{
    private readonly RequestDelegate _next;
    public const string HeaderName = "Idempotency-Key";

    public IdempotencyGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) ||
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method))
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var rawKey))
            {
                var keyValue = rawKey.ToString().Trim();
                if (string.IsNullOrWhiteSpace(keyValue) || keyValue.Length > 128)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";

                    var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
                    var errorResponse = new ApiErrorResponse
                    {
                        Success = false,
                        StatusCode = StatusCodes.Status400BadRequest,
                        ErrorCode = "VAL_INVALID_INPUT",
                        Message = "Idempotency-Key header value is invalid or exceeds maximum length.",
                        TraceId = traceId,
                        Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                    return;
                }

                context.Items[HeaderName] = keyValue;
            }
        }

        await _next(context);
    }
}
