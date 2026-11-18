namespace Gateway.Middlewares.Features;

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Settlement endpoints of AuctionService that only CommerceService calls, directly and not through the Gateway.
    /// Forwarding them would let any client read winners or report an auction order as paid.
    /// </summary>
    private static readonly Regex ServiceOnlyAuctionSettlementPath = new(
        @"^/api/v1/auctions/[^/]+/settlement(/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.HasValue && ServiceOnlyAuctionSettlementPath.IsMatch(path.Value!))
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "NOT_FOUND", "The requested resource was not found.");
            return;
        }

        // The settlement list shows every winner and order, so it is for admin and staff screens only.
        if (path.StartsWithSegments("/api/v1/admin") || path.StartsWithSegments("/api/v1/auction-settlements"))
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
        context.Response.ContentType = "application/problem+json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var (title, type) = statusCode switch
        {
            StatusCodes.Status401Unauthorized => ("Unauthorized", "https://tools.ietf.org/html/rfc9110#section-15.5.3"),
            StatusCodes.Status404NotFound => ("Not Found", "https://tools.ietf.org/html/rfc9110#section-15.5.5"),
            _ => ("Forbidden", "https://tools.ietf.org/html/rfc9110#section-15.5.4")
        };

        var errorResponse = new ApiErrorResponse
        {
            Type = type,
            Title = title,
            Status = statusCode,
            ErrorCode = errorCode,
            Detail = message,
            TraceId = traceId,
            Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        var json = JsonSerializer.Serialize(errorResponse);
        await context.Response.WriteAsync(json);
    }
}
