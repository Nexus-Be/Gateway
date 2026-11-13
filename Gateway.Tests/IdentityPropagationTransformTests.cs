namespace Gateway.Tests;

using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Features;
using Gateway.Middlewares.Global;
using Gateway.Transforms;
using Microsoft.AspNetCore.Http;
using Xunit;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

public sealed class IdentityPropagationTransformTests
{
    [Fact]
    public async Task ApplyTransform_AuthenticatedUser_InjectsExpectedHeaders()
    {
        // Arrange
        var builderContext = new TransformBuilderContext();
        builderContext.AddIdentityPropagation();

        builderContext.RequestTransforms.Count.Should().Be(1);
        var transform = builderContext.RequestTransforms[0];

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        httpContext.Items[ClientContextMiddleware.ItemKeyTraceId] = "trace-12345";
        httpContext.Items[IdempotencyGuardMiddleware.HeaderName] = "idemp-999";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new Claim(ClaimTypes.Email, "buyer@example.com"),
            new Claim(ClaimTypes.Role, "buyer"),
            new Claim("permission", "ORDER_CREATE"),
            new Claim("sid", "33333333-3333-3333-3333-333333333333")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var proxyRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8001/api/v1/orders");
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        // Act
        await transform.ApplyAsync(transformContext);

        // Assert
        proxyRequest.Headers.GetValues("x-client-ip").Should().ContainSingle().Which.Should().Be("10.0.0.5");
        proxyRequest.Headers.GetValues("x-trace-id").Should().ContainSingle().Which.Should().Be("trace-12345");
        proxyRequest.Headers.GetValues("x-user-id").Should().ContainSingle().Which.Should().Be("11111111-1111-1111-1111-111111111111");
        proxyRequest.Headers.GetValues("x-user-email").Should().ContainSingle().Which.Should().Be("buyer@example.com");
        proxyRequest.Headers.GetValues("x-user-roles").Should().ContainSingle().Which.Should().Be("buyer");
        proxyRequest.Headers.GetValues("x-user-permissions").Should().ContainSingle().Which.Should().Be("ORDER_CREATE");
        proxyRequest.Headers.GetValues("x-session-id").Should().ContainSingle().Which.Should().Be("33333333-3333-3333-3333-333333333333");
        proxyRequest.Headers.GetValues("Idempotency-Key").Should().ContainSingle().Which.Should().Be("idemp-999");
    }

    [Fact]
    public async Task ApplyTransform_UnauthenticatedUser_DoesNotInjectUserHeaders()
    {
        // Arrange
        var builderContext = new TransformBuilderContext();
        builderContext.AddIdentityPropagation();

        var transform = builderContext.RequestTransforms[0];
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost:8002/api/v1/products");
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        // Act
        await transform.ApplyAsync(transformContext);

        // Assert
        proxyRequest.Headers.Contains("x-user-id").Should().BeFalse();
        proxyRequest.Headers.Contains("x-user-email").Should().BeFalse();
        proxyRequest.Headers.Contains("x-user-roles").Should().BeFalse();
        proxyRequest.Headers.Contains("x-client-ip").Should().BeTrue();
    }

    [Fact]
    public async Task ApplyTransform_UserIdClaim_InjectsUserIdHeader()
    {
        // Arrange
        var builderContext = new TransformBuilderContext();
        builderContext.AddIdentityPropagation();

        var transform = builderContext.RequestTransforms[0];
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.9");
        httpContext.Items[ClientContextMiddleware.ItemKeyTraceId] = "trace-userId";

        var claims = new[]
        {
            new Claim("userId", "22222222-2222-2222-2222-222222222222"),
            new Claim("email", "seller@example.com"),
            new Claim("role", "seller")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var proxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost:8002/api/v1/products");
        var transformContext = new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = proxyRequest
        };

        // Act
        await transform.ApplyAsync(transformContext);

        // Assert
        proxyRequest.Headers.GetValues("x-user-id").Should().ContainSingle().Which.Should().Be("22222222-2222-2222-2222-222222222222");
        proxyRequest.Headers.GetValues("x-user-email").Should().ContainSingle().Which.Should().Be("seller@example.com");
        proxyRequest.Headers.GetValues("x-user-roles").Should().ContainSingle().Which.Should().Be("seller");
    }
}
