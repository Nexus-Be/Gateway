namespace Gateway.Tests;

using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public sealed class SwaggerAggregationTests
{
    private const string DownstreamDocument = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Nexus Wallet Service REST API", "version": "v1" },
          "paths": {
            "/api/v1/wallets/me": { "get": {} },
            "/api/v1/internal/wallets/checkout/direct": { "post": {} }
          },
          "components": {
            "securitySchemes": {
              "Bearer": { "type": "http", "scheme": "bearer" },
              "x-user-id": { "type": "apiKey", "in": "header", "name": "x-user-id" },
              "x-user-roles": { "type": "apiKey", "in": "header", "name": "x-user-roles" },
              "x-user-email": { "type": "apiKey", "in": "header", "name": "x-user-email" },
              "x-user-permissions": { "type": "apiKey", "in": "header", "name": "x-user-permissions" },
              "X-Internal-Service-Key": { "type": "apiKey", "in": "header", "name": "X-Internal-Service-Key" }
            }
          },
          "security": [
            { "Bearer": [] },
            { "x-user-id": [] },
            { "x-user-roles": [] },
            { "X-Internal-Service-Key": [] }
          ]
        }
        """;

    [Fact]
    public void SanitizeDocument_RemovesIdentityAndInternalSchemes_ButKeepsBearer()
    {
        var sanitized = SwaggerExtensions.SanitizeDocument(DownstreamDocument);

        using var document = JsonDocument.Parse(sanitized);
        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        schemes.TryGetProperty("Bearer", out _).Should().BeTrue();
        schemes.TryGetProperty("x-user-id", out _).Should().BeFalse();
        schemes.TryGetProperty("x-user-roles", out _).Should().BeFalse();
        schemes.TryGetProperty("x-user-email", out _).Should().BeFalse();
        schemes.TryGetProperty("x-user-permissions", out _).Should().BeFalse();
        schemes.TryGetProperty("X-Internal-Service-Key", out _).Should().BeFalse();
    }

    [Fact]
    public void SanitizeDocument_KeepsLegacyInternalPaths_WhenPathStrippingIsDisabled()
    {
        var sanitized = SwaggerExtensions.SanitizeDocument(DownstreamDocument);

        using var document = JsonDocument.Parse(sanitized);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/v1/internal/wallets/checkout/direct", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/wallets/me", out _).Should().BeTrue();
    }

    [Fact]
    public void SanitizeDocument_TrimsGlobalSecurityRequirements_ToBearerOnly()
    {
        var sanitized = SwaggerExtensions.SanitizeDocument(DownstreamDocument);

        using var document = JsonDocument.Parse(sanitized);
        var security = document.RootElement.GetProperty("security");

        security.GetArrayLength().Should().Be(1);
        security[0].TryGetProperty("Bearer", out _).Should().BeTrue();
    }

    [Fact]
    public void SanitizeDocument_ReturnsInputUnchanged_WhenPayloadIsNotJson()
    {
        const string notJson = "<html>downstream error</html>";

        SwaggerExtensions.SanitizeDocument(notJson).Should().Be(notJson);
    }

    [Fact]
    public void DownstreamCatalog_CoversEveryRoutedCluster()
    {
        SwaggerExtensions.ServiceClusters.Keys.Should().BeEquivalentTo(new[]
        {
            "user", "catalog", "commerce", "log", "auction",
            "fulfillment", "notification", "wallet", "checkheal"
        });

        foreach (var service in SwaggerExtensions.ServiceClusters.Keys)
        {
            SwaggerExtensions.DownstreamDisplayNames.Should().ContainKey(service);
        }
    }

    [Fact]
    public async Task DownstreamDocumentEndpoint_Returns502_WhenServiceIsUnknown()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/downstream/does-not-exist/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("SWAGGER_UPSTREAM_UNAVAILABLE");
    }
}
