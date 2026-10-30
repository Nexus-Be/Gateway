namespace Gateway.Tests;

using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Global;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class ClientContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_StripsUntrustedIdentityHeaders()
    {
        // Arrange
        var middleware = new ClientContextMiddleware(context => Task.CompletedTask);
        var context = new DefaultHttpContext();

        context.Request.Headers["x-user-id"] = "forged-user-id";
        context.Request.Headers["x-user-roles"] = "forged-admin";
        context.Request.Headers["x-user-email"] = "spoofed@example.com";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Request.Headers.ContainsKey("x-user-id").Should().BeFalse();
        context.Request.Headers.ContainsKey("x-user-roles").Should().BeFalse();
        context.Request.Headers.ContainsKey("x-user-email").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SetsClientIpAndTraceIdInItems()
    {
        // Arrange
        var middleware = new ClientContextMiddleware(context => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items[ClientContextMiddleware.ItemKeyClientIp].Should().Be("192.168.1.100");
        context.Items.ContainsKey(ClientContextMiddleware.ItemKeyTraceId).Should().BeTrue();
    }
}
