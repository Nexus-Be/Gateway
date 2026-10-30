namespace Gateway.Middlewares.Features;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Gateway.Middlewares.Global;
using Gateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public sealed class SensitiveRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SensitiveRateLimitMiddleware> _logger;

    public record RateLimitRule(int MaxRequests, int WindowSeconds);

    private static readonly Dictionary<string, RateLimitRule> SensitiveRules = new(StringComparer.OrdinalIgnoreCase)
    {
        { "POST:/api/v1/auth/resend-otp", new RateLimitRule(MaxRequests: 3, WindowSeconds: 60) },
        { "POST:/api/v1/auth/verify-otp", new RateLimitRule(MaxRequests: 5, WindowSeconds: 60) },
        { "POST:/api/v1/auth/register", new RateLimitRule(MaxRequests: 5, WindowSeconds: 300) },
        { "POST:/api/v1/auth/login", new RateLimitRule(MaxRequests: 10, WindowSeconds: 60) }
    };

    // In-memory fallback if Redis is unavailable or unconfigured
    private static readonly ConcurrentDictionary<string, (long Count, long Window)> MemoryStore = new();

    public SensitiveRateLimitMiddleware(RequestDelegate next, ILogger<SensitiveRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        var method = context.Request.Method.ToUpperInvariant();
        var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var routeKey = $"{method}:{path}";

        if (!SensitiveRules.TryGetValue(routeKey, out var rule))
        {
            await _next(context);
            return;
        }

        var clientIp = context.Items[ClientContextMiddleware.ItemKeyClientIp]?.ToString()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown_ip";

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentWindow = now / rule.WindowSeconds;

        var redis = serviceProvider.GetService(typeof(IConnectionMultiplexer)) as IConnectionMultiplexer;
        long currentCount;

        if (redis != null && redis.IsConnected)
        {
            try
            {
                var db = redis.GetDatabase();
                var redisKey = $"nexus:gateway:rate:strict:{routeKey}:{clientIp}:{currentWindow}";
                currentCount = await db.StringIncrementAsync(redisKey);
                if (currentCount == 1)
                {
                    await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(rule.WindowSeconds + 2));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis rate limiting failed, falling back to in-memory tracking.");
                currentCount = IncrementInMemory(routeKey, clientIp, currentWindow);
            }
        }
        else
        {
            currentCount = IncrementInMemory(routeKey, clientIp, currentWindow);
        }

        if (currentCount > rule.MaxRequests)
        {
            var remainingSeconds = rule.WindowSeconds - (now % rule.WindowSeconds);
            _logger.LogWarning("Strict rate limit exceeded for {Route} from {IP}. Count: {Count}", routeKey, clientIp, currentCount);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers.RetryAfter = Math.Max(1, remainingSeconds).ToString();

            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var errorResponse = new ApiErrorResponse
            {
                Success = false,
                StatusCode = StatusCodes.Status429TooManyRequests,
                ErrorCode = "RATE_LIMIT_EXCEEDED",
                Message = "Too many requests. Please slow down and try again later.",
                TraceId = traceId,
                Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var json = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }

    private static long IncrementInMemory(string routeKey, string clientIp, long currentWindow)
    {
        var key = $"{routeKey}:{clientIp}";
        var record = MemoryStore.AddOrUpdate(
            key,
            _ => (1, currentWindow),
            (_, old) => old.Window == currentWindow ? (old.Count + 1, currentWindow) : (1, currentWindow));
        return record.Count;
    }
}
