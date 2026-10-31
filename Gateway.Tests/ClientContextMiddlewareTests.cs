namespace Gateway.Tests;

using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Global;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class ClientContextMiddlewareTests
{
    [Theory]
    [InlineData("x-user-id", "forged-user-id")]
    [InlineData("x-user-roles", "forged-admin")]
    [InlineData("x-user-email", "spoofed@example.com")]
    [InlineData("x-user-permissions", "ALL_PERMISSIONS")]
    [InlineData("x-internal-service-key", "spoofed-key")]
    public async Task InvokeAsync_BlocksUntrustedIdentityHeaders_WithForbidden(string headerKey, string headerValue)
    {
        // Arrange
        var nextInvoked = false;
        var middleware = new ClientContextMiddleware(context =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Response.Body = new System.IO.MemoryStream();
        context.Request.Headers[headerKey] = headerValue;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextInvoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        context.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("FORBIDDEN_HEADER_SPOOFING");
    }

    [Theory]
    [InlineData("/api/v1/internal/inventories")]
    [InlineData("/api/v1/internal/email/otp")]
    [InlineData("/services/internal/debug")]
    public async Task InvokeAsync_BlocksInternalEndpoints_WithNotFound(string internalPath)
    {
        // Arrange
        var nextInvoked = false;
        var middleware = new ClientContextMiddleware(context =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = internalPath;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextInvoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
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
