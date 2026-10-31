namespace Gateway.Transforms;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Gateway.Middlewares.Features;
using Gateway.Middlewares.Global;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

public static class IdentityPropagationTransform
{
    public static TransformBuilderContext AddIdentityPropagation(this TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var httpContext = transformContext.HttpContext;
            var proxyRequest = transformContext.ProxyRequest;

            // 1. Forward Client IP
            var clientIp = httpContext.Items[ClientContextMiddleware.ItemKeyClientIp]?.ToString()
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "127.0.0.1";

            proxyRequest.Headers.Remove("x-client-ip");
            proxyRequest.Headers.TryAddWithoutValidation("x-client-ip", clientIp);

            // 2. Forward Trace Identifier
            var traceId = httpContext.Items[ClientContextMiddleware.ItemKeyTraceId]?.ToString()
                ?? httpContext.TraceIdentifier;

            proxyRequest.Headers.Remove("x-trace-id");
            proxyRequest.Headers.TryAddWithoutValidation("x-trace-id", traceId);

            // 3. Forward Authenticated User Claims
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                var user = httpContext.User;

                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst("sub")?.Value
                    ?? user.FindFirst("id")?.Value
                    ?? user.FindFirst("userId")?.Value;

                if (!string.IsNullOrWhiteSpace(userId))
                {
                    proxyRequest.Headers.Remove("x-user-id");
                    proxyRequest.Headers.TryAddWithoutValidation("x-user-id", userId);
                }

                var email = user.FindFirst(ClaimTypes.Email)?.Value
                    ?? user.FindFirst("email")?.Value;

                if (!string.IsNullOrWhiteSpace(email))
                {
                    proxyRequest.Headers.Remove("x-user-email");
                    proxyRequest.Headers.TryAddWithoutValidation("x-user-email", email);
                }

                var roles = user.FindAll(ClaimTypes.Role)
                    .Concat(user.FindAll("role"))
                    .Concat(user.FindAll("roles"))
                    .Select(c => c.Value.ToLowerInvariant())
                    .Distinct()
                    .ToList();

                if (roles.Count > 0)
                {
                    proxyRequest.Headers.Remove("x-user-roles");
                    proxyRequest.Headers.TryAddWithoutValidation("x-user-roles", string.Join(",", roles));
                }

                var permissions = user.FindAll("permission")
                    .Concat(user.FindAll("permissions"))
                    .Select(c => c.Value.ToUpperInvariant())
                    .Distinct()
                    .ToList();

                if (permissions.Count > 0)
                {
                    proxyRequest.Headers.Remove("x-user-permissions");
                    proxyRequest.Headers.TryAddWithoutValidation("x-user-permissions", string.Join(",", permissions));
                }
            }

            // 4. Forward Idempotency Key if validated
            if (httpContext.Items.TryGetValue(IdempotencyGuardMiddleware.HeaderName, out var idempVal) && idempVal is string key)
            {
                proxyRequest.Headers.Remove(IdempotencyGuardMiddleware.HeaderName);
                proxyRequest.Headers.TryAddWithoutValidation(IdempotencyGuardMiddleware.HeaderName, key);
            }

            return ValueTask.CompletedTask;
        });

        return context;
    }
}
