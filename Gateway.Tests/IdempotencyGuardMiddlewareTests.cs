namespace Gateway.Tests;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Features;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class IdempotencyGuardMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PostWithValidKey_StoresInItemsAndCallsNext()
    {
        // Arrange
        var calledNext = false;
        var middleware = new IdempotencyGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/wallets/transfer";
        var validKey = Guid.NewGuid().ToString();
        context.Request.Headers[IdempotencyGuardMiddleware.HeaderName] = validKey;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeTrue();
        context.Items[IdempotencyGuardMiddleware.HeaderName].Should().Be(validKey);
    }

    [Fact]
    public async Task InvokeAsync_PostWithEmptyKey_Returns400BadRequest()
    {
        // Arrange
        var calledNext = false;
        var middleware = new IdempotencyGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/wallets/transfer";
        context.Request.Headers[IdempotencyGuardMiddleware.HeaderName] = "   ";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("VAL_INVALID_INPUT");
    }

    [Fact]
    public async Task InvokeAsync_PostWithTooLongKey_Returns400BadRequest()
    {
        // Arrange
        var calledNext = false;
        var middleware = new IdempotencyGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/wallets/transfer";
        context.Request.Headers[IdempotencyGuardMiddleware.HeaderName] = new string('a', 150);
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_GetRequestWithoutKey_CallsNextDirectly()
    {
        // Arrange
        var calledNext = false;
        var middleware = new IdempotencyGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/wallets/me";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeTrue();
    }
}
