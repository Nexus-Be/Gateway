namespace Gateway.Middlewares.Global;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public sealed class ClientContextMiddleware
{
    private readonly RequestDelegate _next;

    public const string ItemKeyClientIp = "ClientIp";
    public const string ItemKeyTraceId = "TraceId";

    public ClientContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Anti-Spoofing: Strip untrusted identity headers sent by external clients
        context.Request.Headers.Remove("x-user-id");
        context.Request.Headers.Remove("x-user-email");
        context.Request.Headers.Remove("x-user-roles");
        context.Request.Headers.Remove("x-user-permissions");

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

        // 3. Resolve Trace Identifier
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Items[ItemKeyTraceId] = traceId;

        // 4. Inject security and tracking headers on response start
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
