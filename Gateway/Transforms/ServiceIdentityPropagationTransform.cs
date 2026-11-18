namespace Gateway.Transforms;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Gateway.Middlewares.Global;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// For routes that carry a verified service token (see <c>AuctionNotificationServiceAuth</c>): the downstream identity is the verified
/// service, never something the caller wrote. The body and the <c>Authorization</c> header are forwarded unchanged so the downstream service
/// can verify the token again on its own.
/// </summary>
public static class ServiceIdentityPropagationTransform
{
    public const string CallerServiceHeader = "X-Caller-Service";

    public static TransformBuilderContext AddServiceIdentityPropagation(this TransformBuilderContext context, string callerService)
    {
        context.AddRequestTransform(transformContext =>
        {
            var httpContext = transformContext.HttpContext;
            var request = transformContext.ProxyRequest;

            var clientIp = httpContext.Items[ClientContextMiddleware.ItemKeyClientIp]?.ToString()
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            request.Headers.Remove("x-client-ip");
            request.Headers.TryAddWithoutValidation("x-client-ip", clientIp);

            var traceId = httpContext.Items[ClientContextMiddleware.ItemKeyTraceId]?.ToString() ?? httpContext.TraceIdentifier;
            request.Headers.Remove("x-trace-id");
            request.Headers.TryAddWithoutValidation("x-trace-id", traceId);
            if (Activity.Current != null && !string.IsNullOrWhiteSpace(Activity.Current.Id))
            {
                request.Headers.Remove("traceparent");
                request.Headers.TryAddWithoutValidation("traceparent", Activity.Current.Id);
            }

            // Whatever the client sent is dropped; the identity comes from the verified token only.
            foreach (var header in new[] { "x-user-id", "x-user-roles", "x-user-role", "x-user-email", "x-user-permissions", "x-user-permission", "x-session-id", CallerServiceHeader })
                request.Headers.Remove(header);

            var subject = httpContext.User.FindFirst("sub")?.Value;
            if (httpContext.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(subject))
            {
                request.Headers.TryAddWithoutValidation("x-user-id", subject);
                request.Headers.TryAddWithoutValidation("x-user-roles", "service");
                request.Headers.TryAddWithoutValidation(CallerServiceHeader, callerService);
            }

            return ValueTask.CompletedTask;
        });

        return context;
    }
}
