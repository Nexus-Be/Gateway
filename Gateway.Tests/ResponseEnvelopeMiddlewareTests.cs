namespace Gateway.Tests;

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Global;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class ResponseEnvelopeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RawJson_WrapsIntoApiResponseEnvelope()
    {
        // Arrange
        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            var rawJson = JsonSerializer.Serialize(new { id = "123", name = "Test Item" });
            await context.Response.WriteAsync(rawJson);
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("statusCode").GetInt32().Should().Be(200);
        root.GetProperty("message").GetString().Should().Be("Operation completed successfully.");
        root.GetProperty("data").GetProperty("id").GetString().Should().Be("123");
        root.GetProperty("data").GetProperty("name").GetString().Should().Be("Test Item");
        root.TryGetProperty("traceId", out _).Should().BeTrue();
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AlreadyEnvelopedJson_PassesThroughUnchanged()
    {
        // Arrange
        var preWrappedJson = JsonSerializer.Serialize(new
        {
            success = true,
            statusCode = 200,
            message = "Custom existing message",
            data = new { id = "456" }
        });

        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(preWrappedJson);
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Custom existing message");
    }

    [Fact]
    public async Task InvokeAsync_NoContent204_RemainsEmpty()
    {
        // Arrange
        var middleware = new ResponseEnvelopeMiddleware(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        context.Response.Body.Length.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_RawJsonWithCustomMessage_UsesCustomMessageInEnvelope()
    {
        // Arrange
        var rawJson = JsonSerializer.Serialize(new
        {
            userId = "11111111-1111-1111-1111-111111111111",
            message = "Password reset link sent to your email."
        });

        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(rawJson);
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Be("Password reset link sent to your email.");
        doc.RootElement.GetProperty("data").GetProperty("userId").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task InvokeAsync_RawDtoWithSuccessProperty_WrapsIntoApiResponseEnvelope()
    {
        // Arrange: Raw DTO returning { success = true } (e.g. LogoutApiResponse)
        var rawJson = JsonSerializer.Serialize(new { success = true });

        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(rawJson);
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("statusCode").GetInt32().Should().Be(200);
        root.GetProperty("message").GetString().Should().Be("Operation completed successfully.");
        root.GetProperty("data").GetProperty("success").GetBoolean().Should().BeTrue();
        root.TryGetProperty("traceId", out _).Should().BeTrue();
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SwaggerPath_PassesThroughWithoutEnvelope()
    {
        const string openApiDocument = "{\"openapi\":\"3.0.1\",\"paths\":{}}";

        var middleware = new ResponseEnvelopeMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(openApiDocument);
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger/v1/swagger.json";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync()).Should().Be(openApiDocument);
    }
}
