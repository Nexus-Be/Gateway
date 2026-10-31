namespace Gateway.Middlewares.Global;

using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Gateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class ClientContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClientContextMiddleware>? _logger;

    public const string ItemKeyClientIp = "ClientIp";
    public const string ItemKeyTraceId = "TraceId";
    public const string HeaderKeyInternalService = "x-internal-service-key";

    private static readonly string[] ForbiddenSpoofedHeaders = new[]
    {
        "x-user-id",
        "x-user-email",
        "x-user-roles",
        "x-user-role",
        "x-user-permissions",
        "x-user-permission",
        HeaderKeyInternalService
    };

    public ClientContextMiddleware(RequestDelegate next, ILogger<ClientContextMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Resolve Trace Identifier first for logging and error envelope
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Items[ItemKeyTraceId] = traceId;

        // 2. Resolve Client IP
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.ToString().Split(',')[0].Trim();
                clientIp = first;
            }
            else
            {
                clientIp = "127.0.0.1";
            }
        }
        context.Items[ItemKeyClientIp] = clientIp;

        // 3. Active Anti-Spoofing: Check if incoming request contains any forbidden identity or internal headers
        foreach (var headerKey in ForbiddenSpoofedHeaders)
        {
            if (context.Request.Headers.ContainsKey(headerKey))
            {
                _logger?.LogWarning(
                    "[SECURITY VIOLATION] Blocked spoofed header '{HeaderKey}' from Client IP: '{ClientIp}', TraceId: '{TraceId}', Path: '{Path}'",
                    headerKey, clientIp, traceId, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                var errorResponse = new ApiErrorResponse
                {
                    Success = false,
                    StatusCode = StatusCodes.Status403Forbidden,
                    ErrorCode = "FORBIDDEN_HEADER_SPOOFING",
                    Message = "Security violation: Untrusted identity header detected in external request.",
                    TraceId = traceId,
                    Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }
        }

        // 4. Reject attempts to call internal service APIs via Gateway
        if (context.Request.Path.StartsWithSegments("/api/v1/internal", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.Value?.Contains("/internal/", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var notFoundResponse = new ApiErrorResponse
            {
                Success = false,
                StatusCode = StatusCodes.Status404NotFound,
                ErrorCode = "NOT_FOUND",
                Message = "Resource not found.",
                TraceId = traceId,
                Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(notFoundResponse));
            return;
        }

        // 5. Inject security and tracking headers on response start
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
