namespace Gateway.Tests;

using System;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Features;
using Gateway.Middlewares.Global;
using Gateway.Transforms;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

/// <summary>
/// Contract Testing Suite for Edge Gateway Invariants defined in specs/06.
/// Verifies that all architectural invariants hold unconditionally at the Gateway boundary.
/// </summary>
public sealed class ArchitecturalContractInvariantTests
{
    [Theory]
    [InlineData("x-user-id", "evil-attacker-guid")]
    [InlineData("X-USER-ID", "evil-attacker-guid")]
    [InlineData("x-user-email", "admin@victim.com")]
    [InlineData("X-User-Email", "admin@victim.com")]
    [InlineData("x-user-roles", "admin,superuser")]
    [InlineData("x-user-role", "admin")]
    [InlineData("x-user-permissions", "ALL_PERMISSIONS")]
    [InlineData("x-user-permission", "SUPER_ADMIN")]
    [InlineData("x-internal-service-key", "forged-secret-key")]
    [InlineData("X-Internal-Service-Key", "forged-secret-key")]
    public async Task Invariant_AntiHeaderSpoofing_AbortsPipeline_With403ForbiddenHeaderSpoofing(string headerKey, string headerValue)
    {
        // Invariant 2: ClientContextMiddleware MUST actively reject untrusted identity headers with 403
        var nextCalled = false;
        var middleware = new ClientContextMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[headerKey] = headerValue;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse("Pipeline must abort immediately when untrusted identity headers are detected");
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("statusCode").GetInt32().Should().Be(403);
        root.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN_HEADER_SPOOFING");
    }

    [Theory]
    [InlineData("/api/v1/internal/inventories/initialize")]
    [InlineData("/api/v1/internal/notifications/email/otp")]
    [InlineData("/api/v1/internal/wallets/checkout/direct")]
    [InlineData("/api/v1/internal/logs/batch")]
    [InlineData("/internal/secret-debug")]
    public async Task Invariant_InternalRoutes_AreCompletelyBlocked_With404NotFound(string internalPath)
    {
        // Invariant 3: External requests to internal service-to-service endpoints MUST be rejected with 404
        var nextCalled = false;
        var middleware = new ClientContextMiddleware(context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = internalPath;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse("Internal routes must never reach downstream proxies via Gateway");
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Invariant_IdentityPropagation_InjectsValidatedClaims_ToDownstreamHeaders()
    {
        // Invariant 4: Zero-Trust identity propagation extracts validated JWT claims into headers
        var builderContext = new TransformBuilderContext();
        builderContext.AddIdentityPropagation();
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        httpContext.Items[ClientContextMiddleware.ItemKeyTraceId] = "w3c-trace-001";
        httpContext.Items[IdempotencyGuardMiddleware.HeaderName] = "idemp-uuid-v4";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            new Claim(ClaimTypes.Email, "verified.user@nexus.local"),
            new Claim(ClaimTypes.Role, "seller"),
            new Claim("permission", "PRODUCT_CREATE"),
            new Claim("permission", "INVENTORY_VIEW")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var proxyRequest = new HttpRequestMessage(HttpMethod.Post, "http://catalog-service:8002/api/v1/products");
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        await transform.ApplyAsync(transformContext);

        proxyRequest.Headers.GetValues("x-client-ip").Should().ContainSingle().Which.Should().Be("192.168.1.100");
        proxyRequest.Headers.GetValues("x-trace-id").Should().ContainSingle().Which.Should().Be("w3c-trace-001");
        proxyRequest.Headers.GetValues("x-user-id").Should().ContainSingle().Which.Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        proxyRequest.Headers.GetValues("x-user-email").Should().ContainSingle().Which.Should().Be("verified.user@nexus.local");
        proxyRequest.Headers.GetValues("x-user-roles").Should().ContainSingle().Which.Should().Be("seller");
        proxyRequest.Headers.GetValues("x-user-permissions").Should().ContainSingle().Which.Should().Be("PRODUCT_CREATE,INVENTORY_VIEW");
        proxyRequest.Headers.GetValues("Idempotency-Key").Should().ContainSingle().Which.Should().Be("idemp-uuid-v4");
    }

    [Fact]
    public async Task Invariant_ResponseEnvelope_WrapsRawJsonPayload_IntoApiResponseEnvelope()
    {
        // Invariant 5: Raw downstream 200/201 JSON is automatically wrapped into ApiResponse<T>
        var rawPayload = JsonSerializer.Serialize(new
        {
            productId = "prod-999",
            name = "Wireless Keyboard",
            price = 450000.00m
        });

        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(rawPayload);
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseJson = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("statusCode").GetInt32().Should().Be(200);
        root.GetProperty("message").GetString().Should().Be("Operation completed successfully.");
        root.GetProperty("data").GetProperty("productId").GetString().Should().Be("prod-999");
        root.GetProperty("data").GetProperty("price").GetDecimal().Should().Be(450000.00m);
    }

    [Fact]
    public async Task Invariant_ResponseEnvelope_Preserves204NoContent_WithoutWrapping()
    {
        // Invariant 5: HTTP 204 NoContent MUST pass through untouched without JSON body
        var middleware = new ResponseEnvelopeMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        context.Response.Body.Length.Should().Be(0, "HTTP 204 NoContent must have zero body length");
    }

    [Fact]
    public async Task Invariant_ResponseEnvelope_IsIdempotent_WhenPayloadAlreadyHasSuccessProperty()
    {
        // Invariant 5: Pre-wrapped responses must NOT be double wrapped
        var preWrappedJson = JsonSerializer.Serialize(new
        {
            success = true,
            statusCode = 200,
            message = "Pre-wrapped message.",
            data = new { id = "item-1" }
        });

        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(preWrappedJson);
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseJson = await reader.ReadToEndAsync();

        responseJson.Should().Be(preWrappedJson, "Idempotent wrapping must prevent double wrapping");
    }
}
