namespace Gateway.Tests;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Global;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_CatchesAndReturns500ApiErrorResponse()
    {
        // Arrange
        var middleware = new GlobalExceptionMiddleware(
            context => throw new InvalidOperationException("Simulated database failure"),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
        root.GetProperty("statusCode").GetInt32().Should().Be(500);
        root.GetProperty("errorCode").GetString().Should().Be("INTERNAL_SERVER_ERROR");
        root.GetProperty("message").GetString().Should().Be("An unexpected error occurred while processing your request.");
        root.TryGetProperty("traceId", out _).Should().BeTrue();
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_CallsNextSuccessfully()
    {
        // Arrange
        var calledNext = false;
        var middleware = new GlobalExceptionMiddleware(
            context =>
            {
                calledNext = true;
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
