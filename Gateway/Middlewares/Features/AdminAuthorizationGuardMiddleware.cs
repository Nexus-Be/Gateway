namespace Gateway.Middlewares.Features;

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Gateway.Models;
using Microsoft.AspNetCore.Http;

public sealed class AdminAuthorizationGuardMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthorizationGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/admin"))
        {
            var user = context.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Authentication is required to access administrative endpoints.");
                return;
            }

            var hasAdminRole = user.FindAll(ClaimTypes.Role)
                .Concat(user.FindAll("role"))
                .Concat(user.FindAll("roles"))
                .Select(c => c.Value.ToLowerInvariant())
                .Any(r => r == "admin" || r == "staff" || r == "administrator");

            if (!hasAdminRole)
            {
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "FORBIDDEN", "You do not have permission to access administrative endpoints.");
                return;
            }
        }

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var errorResponse = new ApiErrorResponse
        {
            Success = false,
            StatusCode = statusCode,
            ErrorCode = errorCode,
            Message = message,
            TraceId = traceId,
            Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }
}
