namespace Gateway.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

/// <summary>
/// The notifications that AuctionService sends reach NotificationService through the Gateway with a service token of their own.
/// These tests run the real pipeline against a stand-in downstream service and check who gets through, and which identity the downstream sees.
/// </summary>
public sealed class AuctionNotificationServiceAuthTests : IAsyncLifetime
{
    private const string ServiceKey = "unit-test-auction-notification-signing-key-0123456789";
    private const string UserKey = "unit-test-user-jwt-signing-key-for-the-gateway-0123456789";
    private static readonly Guid ServiceId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    public static readonly string[] ServiceRoutes =
    {
        "/api/v1/notifications/auction-failed",
        "/api/v1/notifications/auction-cancelled",
        "/api/v1/notifications/auction-winner-offers",
        "/api/v1/notifications/auction-outbids",
        "/api/v1/notifications/auction-winners",
        "/api/v1/notifications/auction-order-payments",
    };

    public static TheoryData<string> Routes { get { var data = new TheoryData<string>(); foreach (var route in ServiceRoutes) data.Add(route); return data; } }

    private sealed record Seen(string Method, string Path, string? UserId, string? Roles, string? Caller, string? Authorization, string? Email, string Body);

    private readonly ConcurrentQueue<Seen> _seen = new();
    private WebApplication _downstream = null!;
    private WebApplicationFactory<Program> _gateway = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _downstream = builder.Build();
        _downstream.Run(async context =>
        {
            string body;
            using (var reader = new System.IO.StreamReader(context.Request.Body)) body = await reader.ReadToEndAsync();
            _seen.Enqueue(new Seen(context.Request.Method, context.Request.Path, context.Request.Headers["x-user-id"], context.Request.Headers["x-user-roles"],
                context.Request.Headers["X-Caller-Service"], context.Request.Headers.Authorization, context.Request.Headers["x-user-email"], body));
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("{}");
        });
        await _downstream.StartAsync();
        var address = _downstream.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        _gateway = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new[]
        {
            KeyValuePair("Jwt:Signature", UserKey), KeyValuePair("Jwt:Issuer", "http://localhost:8001"), KeyValuePair("Jwt:Audience", "shoppingcart-api"),
            KeyValuePair("AuctionNotificationAuth:SigningKey", ServiceKey), KeyValuePair("AuctionNotificationAuth:ServiceUserId", ServiceId.ToString()),
            KeyValuePair("ReverseProxy:Clusters:notification-cluster:Destinations:notification-service:Address", address),
            KeyValuePair("ReverseProxy:Clusters:user-cluster:Destinations:user-service:Address", address),
        })));
        _client = _gateway.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _gateway.DisposeAsync();
        await _downstream.DisposeAsync();
    }

    private static System.Collections.Generic.KeyValuePair<string, string?> KeyValuePair(string key, string value) => new(key, value);

    private static string Sign(string key, string issuer, string audience, string subject, string? scope, DateTime? expires = null, params Claim[] extra)
    {
        var now = DateTime.UtcNow;
        var claims = new System.Collections.Generic.List<Claim> { new("sub", subject) };
        if (scope is not null) claims.Add(new Claim("scope", scope));
        claims.AddRange(extra);
        var token = new JwtSecurityToken(issuer, audience, claims, now.AddMinutes(-3), expires ?? now.AddMinutes(2),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string ServiceToken(string? scope = "auction.notifications.write", Guid? subject = null, DateTime? expires = null, string? key = null,
        string issuer = "auction-service", string audience = "nexus-auction-notifications") =>
        Sign(key ?? ServiceKey, issuer, audience, (subject ?? ServiceId).ToString(), scope, expires);

    private static string UserToken(string role = "admin") =>
        Sign(UserKey, "http://localhost:8001", "shoppingcart-api", Guid.NewGuid().ToString(), null, null,
            new Claim("role", role), new Claim("email", "someone@example.com"));

    private async Task<HttpResponseMessage> Post(string path, string? bearer, string? spoofRoles = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent("{\"sourceEventId\":\"1\"}", Encoding.UTF8, "application/json") };
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (spoofRoles is not null) request.Headers.TryAddWithoutValidation("x-user-roles", spoofRoles);
        return await _client.SendAsync(request);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task VerifiedServiceToken_ReachesNotification_WithTheServiceIdentityAndTheOriginalToken(string path)
    {
        var token = ServiceToken();

        var response = await Post(path, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var seen = _seen.Single();
        seen.Path.Should().Be(path);
        seen.UserId.Should().Be(ServiceId.ToString());
        seen.Roles.Should().Be("service");
        seen.Caller.Should().Be("auction-service");
        seen.Authorization.Should().Be("Bearer " + token, "Notification verifies the token again on its own");
        seen.Body.Should().Be("{\"sourceEventId\":\"1\"}", "the body is forwarded unchanged");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task UserTokens_EvenAdmin_AreRefused(string path)
    {
        (await Post(path, UserToken("admin"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Post(path, UserToken("service"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Post(path, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _seen.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ServiceTokens_ThatAreNotExactlyAuctions_AreRefused(string path)
    {
        var bad = new[]
        {
            ServiceToken(key: "another-signing-key-another-signing-key-1234"),
            ServiceToken(issuer: "someone-else"),
            ServiceToken(audience: "shoppingcart-api"),
            ServiceToken(expires: DateTime.UtcNow.AddMinutes(-1)),
            "not-a-token",
        };
        foreach (var token in bad) (await Post(path, token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, token);

        (await Post(path, ServiceToken(scope: "other.scope"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(path, ServiceToken(scope: null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(path, ServiceToken(subject: Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _seen.Should().BeEmpty();
    }

    [Fact]
    public async Task AServiceToken_NeverBecomesAnIdentityOnUserRoutes()
    {
        var token = ServiceToken();

        // Admin routes are guarded at the Gateway: a service token is not an admin.
        (await _client.SendAsync(Get("/api/v1/admin/users", token))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Other routes are only forwarded; the user scheme rejects the token, so the downstream gets no identity at all.
        foreach (var path in new[] { "/api/v1/notifications", "/api/v1/me/profile" })
        {
            _seen.Clear();
            await _client.SendAsync(Get(path, token));
            var seen = _seen.Single();
            seen.UserId.Should().BeNullOrEmpty();
            seen.Roles.Should().BeNullOrEmpty();
            seen.Caller.Should().BeNullOrEmpty();
        }
    }

    [Fact]
    public async Task OnlyPost_UsesTheServicePolicy_AndTheUserRoutesKeepTheirOwnBehaviour()
    {
        var token = ServiceToken();
        _seen.Clear();

        await _client.SendAsync(Get("/api/v1/notifications/auction-failed", token));

        var seen = _seen.Single();
        seen.Caller.Should().BeNullOrEmpty("a GET on the same path is a user route, not a service call");
        seen.Roles.Should().NotBe("service");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ForgedIdentityHeaders_AreStillBlockedBeforeAnyTokenIsLookedAt(string path)
    {
        var response = await Post(path, ServiceToken(), spoofRoles: "service");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _seen.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutASigningKey_NoServiceTokenIsEverAccepted()
    {
        await using var unconfigured = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new[]
        {
            KeyValuePair("Jwt:Signature", UserKey), KeyValuePair("Jwt:Issuer", "http://localhost:8001"), KeyValuePair("Jwt:Audience", "shoppingcart-api"),
            KeyValuePair("AuctionNotificationAuth:SigningKey", "short-key-16-bytes"),
        })));
        using var client = unconfigured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ServiceRoutes[0]) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceToken());

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static HttpRequestMessage Get(string path, string bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }
}
