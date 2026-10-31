namespace Gateway.Tests;

using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public sealed class GatewayRootTests
{
    [Fact]
    public async Task Root_RedirectsToSwagger_AtDevelopment()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/swagger");
    }

    [Fact]
    public async Task SwaggerDocument_IsServedWithoutResponseEnvelope()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        root.TryGetProperty("openapi", out _).Should().BeTrue();
        root.TryGetProperty("success", out _).Should().BeFalse();
        root.TryGetProperty("data", out _).Should().BeFalse();
    }
}
