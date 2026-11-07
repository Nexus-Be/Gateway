namespace Gateway.Tests;

using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Configuration;

public sealed class RouteCatalogConfigurationTests
{
    [Theory]
    [InlineData("categories-route", "/api/v1/categories/{**catch-all}", "catalog-cluster")]
    [InlineData("products-route", "/api/v1/products/{**catch-all}", "catalog-cluster")]
    [InlineData("variants-route", "/api/v1/variants/{**catch-all}", "catalog-cluster")]
    [InlineData("images-route", "/api/v1/images/{**catch-all}", "catalog-cluster")]
    [InlineData("seller-products-route", "/api/v1/seller/products/{**catch-all}", "catalog-cluster")]
    public async Task CatalogRoutes_AreConfiguredCorrectly_InReverseProxy(string routeId, string expectedPath, string expectedCluster)
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfigProvider = factory.Services.GetRequiredService<IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        var route = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == routeId);
        route.Should().NotBeNull($"Route '{routeId}' must be configured in Gateway YARP reverse proxy");
        route!.ClusterId.Should().Be(expectedCluster);
        route.Match.Path.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("auth-route", "/api/v1/auth/{**catch-all}", "user-cluster")]
    [InlineData("user-me-route", "/api/v1/me/{**catch-all}", "user-cluster")]
    [InlineData("users-route", "/api/v1/users/{**catch-all}", "user-cluster")]
    [InlineData("sellers-route", "/api/v1/sellers/{**catch-all}", "user-cluster")]
    [InlineData("admin-users-route", "/api/v1/admin/users/{**catch-all}", "user-cluster")]
    [InlineData("admin-roles-route", "/api/v1/admin/roles/{**catch-all}", "user-cluster")]
    [InlineData("admin-seller-registrations-route", "/api/v1/admin/seller-registrations/{**catch-all}", "user-cluster")]
    [InlineData("admin-sellers-route", "/api/v1/admin/sellers/{**catch-all}", "user-cluster")]
    [InlineData("admin-penalties-route", "/api/v1/admin/penalties/{**catch-all}", "user-cluster")]
    [InlineData("admin-reports-route", "/api/v1/admin/reports/{**catch-all}", "user-cluster")]
    [InlineData("admin-reviews-route", "/api/v1/admin/reviews/{**catch-all}", "user-cluster")]
    [InlineData("admin-configs-route", "/api/v1/admin/configs/{**catch-all}", "user-cluster")]
    [InlineData("reputation-route", "/api/v1/reputation/{**catch-all}", "user-cluster")]
    [InlineData("reviews-route", "/api/v1/reviews/{**catch-all}", "user-cluster")]
    public async Task UserRoutes_AreConfiguredCorrectly_InReverseProxy(string routeId, string expectedPath, string expectedCluster)
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfigProvider = factory.Services.GetRequiredService<IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        var route = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == routeId);
        route.Should().NotBeNull($"Route '{routeId}' must be configured in Gateway YARP reverse proxy");
        route!.ClusterId.Should().Be(expectedCluster);
        route.Match.Path.Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("webhooks-ghn-route", "/api/v1/webhooks/ghn/{**catch-all}", "fulfillment-cluster")]
    public async Task FulfillmentWebhookRoute_IsConfiguredCorrectly_InReverseProxy(string routeId, string expectedPath, string expectedCluster)
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfigProvider = factory.Services.GetRequiredService<IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        var route = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == routeId);
        route.Should().NotBeNull($"Route '{routeId}' must be configured in Gateway YARP reverse proxy");
        route!.ClusterId.Should().Be(expectedCluster);
        route.Match.Path.Should().Be(expectedPath);
    }

    [Fact]
    public async Task ProxyClusters_ContainCatalogAndUserClusters()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfigProvider = factory.Services.GetRequiredService<IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        var clusterIds = proxyConfig.Clusters.Select(c => c.ClusterId).ToList();
        clusterIds.Should().Contain("catalog-cluster");
        clusterIds.Should().Contain("user-cluster");
    }
}
