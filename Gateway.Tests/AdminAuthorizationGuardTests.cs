namespace Gateway.Tests;

using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Features;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class AdminAuthorizationGuardTests
{
    [Fact]
    public async Task InvokeAsync_UnauthenticatedAdminRoute_Returns401()
    {
        // Arrange
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/admin/users";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task InvokeAsync_NonAdminRole_Returns403()
    {
        // Arrange
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/admin/users";
        context.Response.Body = new MemoryStream();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new Claim(ClaimTypes.Role, "buyer")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errorCode").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task InvokeAsync_AdminRole_ProceedsToNext()
    {
        // Arrange
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(context =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/admin/users";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new Claim(ClaimTypes.Role, "admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        calledNext.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/v1/auctions/7d7f0d8e-1111-4222-8333-944444444444/settlement")]
    [InlineData("/api/v1/auctions/7d7f0d8e-1111-4222-8333-944444444444/settlement/payment-status")]
    [InlineData("/API/V1/AUCTIONS/7d7f0d8e-1111-4222-8333-944444444444/Settlement/Payment-Status")]
    public async Task InvokeAsync_ServiceOnlySettlementPath_IsNotForwardedEvenForAdmin(string path)
    {
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(_ => { calledNext = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "TestAuth"));

        await middleware.InvokeAsync(context);

        calledNext.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData(null, StatusCodes.Status401Unauthorized)]
    [InlineData("buyer", StatusCodes.Status403Forbidden)]
    public async Task InvokeAsync_SettlementList_RequiresAdminOrStaff(string? role, int expectedStatus)
    {
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(_ => { calledNext = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/auction-settlements";
        context.Response.Body = new MemoryStream();
        if (role is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "TestAuth"));

        await middleware.InvokeAsync(context);

        calledNext.Should().BeFalse();
        context.Response.StatusCode.Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData("staff")]
    [InlineData("admin")]
    public async Task InvokeAsync_SettlementList_AllowsAdminAndStaff(string role)
    {
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(_ => { calledNext = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/auction-settlements";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "TestAuth"));

        await middleware.InvokeAsync(context);

        calledNext.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_PublicAuctionResult_IsStillForwarded()
    {
        var calledNext = false;
        var middleware = new AdminAuthorizationGuardMiddleware(_ => { calledNext = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/auctions/7d7f0d8e-1111-4222-8333-944444444444/result";

        await middleware.InvokeAsync(context);

        calledNext.Should().BeTrue();
    }
}
