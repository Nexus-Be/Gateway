namespace Gateway.Tests;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public sealed class SensitiveRateLimitMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NonSensitiveRoute_PassesThrough()
    {
        // Arrange
        var calledNext = false;
        var middleware = new SensitiveRateLimitMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        }, NullLogger<SensitiveRateLimitMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/products";

        var sp = new Mock<IServiceProvider>().Object;

        // Act
        await middleware.InvokeAsync(context, sp);

        // Assert
        calledNext.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_ExceedingLimit_Returns429TooManyRequests()
    {
        // Arrange
        var middleware = new SensitiveRateLimitMiddleware(context => Task.CompletedTask, NullLogger<SensitiveRateLimitMiddleware>.Instance);
        var sp = new Mock<IServiceProvider>().Object;
        var ip = $"test-ip-{Guid.NewGuid():N}";

        // Max limit for resend-otp is 3 requests per 60s
        for (var i = 1; i <= 3; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Method = "POST";
            ctx.Request.Path = "/api/v1/auth/resend-otp";
            ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            ctx.Items["ClientIp"] = ip;
            await middleware.InvokeAsync(ctx, sp);
            ctx.Response.StatusCode.Should().Be(200);
        }

        // 4th request must be rate limited
        var blockedContext = new DefaultHttpContext();
        blockedContext.Request.Method = "POST";
        blockedContext.Request.Path = "/api/v1/auth/resend-otp";
        blockedContext.Items["ClientIp"] = ip;
        blockedContext.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(blockedContext, sp);

        // Assert
        blockedContext.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        blockedContext.Response.Headers.ContainsKey("Retry-After").Should().BeTrue();

        blockedContext.Response.Body.Position = 0;
        using var reader = new StreamReader(blockedContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("RATE_LIMIT_EXCEEDED");
    }
}
