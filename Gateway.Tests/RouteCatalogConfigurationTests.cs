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
    [InlineData("admin-products-route", "/api/v1/admin/products/{**catch-all}", "catalog-cluster")]
    [InlineData("admin-product-reports-route", "/api/v1/admin/reports/products/{**catch-all}", "catalog-cluster")]
    [InlineData("admin-product-reviews-moderate-route", "/api/v1/admin/reviews/{id}/moderate", "catalog-cluster")]
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
    [InlineData("reports-route", "/api/v1/reports/{**catch-all}", "user-cluster")]
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
    [InlineData("ghn-webhooks-direct-route", "/webhooks/ghn/{**catch-all}", "fulfillment-cluster")]
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

    [Theory]
    [InlineData("auctions-route", "/api/v1/auctions/{**catch-all}", "auction-cluster")]
    [InlineData("admin-auction-configs-route", "/api/v1/admin/auction-configs/{**catch-all}", "auction-cluster")]
    [InlineData("seller-auctions-route", "/api/v1/seller/auctions/{**catch-all}", "auction-cluster")]
    [InlineData("auction-me-bids-route", "/api/v1/me/bids/{**catch-all}", "auction-cluster")]
    [InlineData("auction-me-deposits-route", "/api/v1/me/deposits/{**catch-all}", "auction-cluster")]
    [InlineData("auction-me-auctions-route", "/api/v1/me/auctions/{**catch-all}", "auction-cluster")]
    [InlineData("auction-me-watchlist-route", "/api/v1/me/watchlist/{**catch-all}", "auction-cluster")]
    [InlineData("auction-seller-reserve-status-route", "/api/v1/seller/reserve-status/{**catch-all}", "auction-cluster")]
    [InlineData("auction-me-offers-route", "/api/v1/me/auction-offers/{**catch-all}", "auction-cluster")]
    [InlineData("auction-settlements-route", "/api/v1/auction-settlements/{**catch-all}", "auction-cluster")]
    [InlineData("admin-auctions-route", "/api/v1/admin/auctions/{**catch-all}", "auction-cluster")]
    [InlineData("seller-auction-statistics-route", "/api/v1/sellers/{sellerId}/auction-statistics", "auction-cluster")]
    public async Task AuctionRoutes_AreConfiguredCorrectly_InReverseProxy(string routeId, string expectedPath, string expectedCluster)
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfigProvider = factory.Services.GetRequiredService<IProxyConfigProvider>();
        var proxyConfig = proxyConfigProvider.GetConfig();

        var route = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == routeId);
        route.Should().NotBeNull($"Route '{routeId}' must be configured in Gateway YARP reverse proxy");
        route!.ClusterId.Should().Be(expectedCluster);
        route.Match.Path.Should().Be(expectedPath);
    }

    /// <summary>
    /// /api/v1/admin/reviews/{id}/moderate belongs to CatalogService (product reviews) but sits under the /api/v1/admin/reviews catch-all of
    /// UserService. With a higher Order number it was evaluated after the catch-all and never reached Catalog.
    /// </summary>
    [Fact]
    public async Task ProductReviewModeration_IsEvaluatedBeforeTheAdminReviewsCatchAll()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var routes = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig().Routes;

        var moderate = routes.First(r => r.RouteId == "admin-product-reviews-moderate-route");
        var catchAll = routes.First(r => r.RouteId == "admin-reviews-route");

        moderate.ClusterId.Should().Be("catalog-cluster");
        (moderate.Order ?? 0).Should().BeLessThan(catchAll.Order ?? 0);
    }

    /// <summary>
    /// /api/v1/admin/reports/products belongs to CatalogService but sits under the /api/v1/admin/reports catch-all of UserService.
    /// With a higher Order number it was evaluated after the catch-all and every call ended in a 404 from UserService.
    /// </summary>
    [Fact]
    public async Task ProductReportModeration_IsEvaluatedBeforeTheAdminReportsCatchAll()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var routes = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig().Routes;

        var productReports = routes.First(r => r.RouteId == "admin-product-reports-route");
        var catchAll = routes.First(r => r.RouteId == "admin-reports-route");

        productReports.ClusterId.Should().Be("catalog-cluster");
        (productReports.Order ?? 0).Should().BeLessThan(catchAll.Order ?? 0);
    }

    /// <summary>
    /// Product reviews live in CatalogService (PUT and DELETE /api/v1/reviews/{id}); partner reviews live in UserService
    /// (POST /api/v1/reviews, its upload and eligibility routes). One path prefix, two services: only the method and the GUID tell them apart.
    /// </summary>
    [Fact]
    public async Task ProductReviewEditAndDelete_GoToCatalog_AndEverythingElseUnderReviewsStaysWithUser()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var routes = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig().Routes;

        var productReviews = routes.FirstOrDefault(r => r.RouteId == "catalog-review-by-id-route");
        var partnerReviews = routes.First(r => r.RouteId == "reviews-route");

        productReviews.Should().NotBeNull();
        productReviews!.ClusterId.Should().Be("catalog-cluster");
        productReviews.Match.Path.Should().Be("/api/v1/reviews/{id:guid}");
        productReviews.Match.Methods.Should().BeEquivalentTo(new[] { "PUT", "DELETE" });   // never POST, GET or the upload
        (productReviews.Order ?? 0).Should().BeLessThan(partnerReviews.Order ?? 0,
            "YARP evaluates lower numbers first, so the narrower route must come before the /reviews catch-all");
        partnerReviews.ClusterId.Should().Be("user-cluster");
        partnerReviews.Match.Path.Should().Be("/api/v1/reviews/{**catch-all}");
    }

    [Theory]
    [InlineData("auction-orders-route", "/api/v1/auction-orders/{auctionOrderId:guid}/{**catch-all}", "commerce-cluster")]
    [InlineData("auction-orders-me-route", "/api/v1/auction-orders/me", "commerce-cluster")]
    [InlineData("auction-orders-seller-route", "/api/v1/auction-orders/seller", "commerce-cluster")]
    [InlineData("discounts-route", "/api/v1/discounts/{**catch-all}", "commerce-cluster")]
    [InlineData("admin-platform-fee-route", "/api/v1/admin/platform-fee/{**catch-all}", "commerce-cluster")]
    [InlineData("shipping-locations-route", "/api/v1/shipping/locations/{**catch-all}", "fulfillment-cluster")]
    [InlineData("auction-hub-route", "/hubs/auctions/{**catch-all}", "auction-cluster")]
    public async Task AuctionOrderAndMissingRoutes_AreConfiguredCorrectly_InReverseProxy(string routeId, string expectedPath, string expectedCluster)
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfig = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        var route = proxyConfig.Routes.FirstOrDefault(r => r.RouteId == routeId);
        route.Should().NotBeNull($"Route '{routeId}' must be configured in Gateway YARP reverse proxy");
        route!.ClusterId.Should().Be(expectedCluster);
        route.Match.Path.Should().Be(expectedPath);
    }

    [Fact]
    public async Task SellerAuctionStatistics_IsEvaluatedBeforeSellersCatchAll()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var routes = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig().Routes;

        var statsRoute = routes.First(r => r.RouteId == "seller-auction-statistics-route");
        var sellersRoute = routes.First(r => r.RouteId == "sellers-route");

        statsRoute.ClusterId.Should().Be("auction-cluster");
        (statsRoute.Order ?? 0).Should().BeLessThan(sellersRoute.Order ?? 0);
    }

    /// <summary>
    /// The settlement endpoint is called by AuctionService only. No route may forward it, so a client cannot create an auction order.
    /// </summary>
    [Fact]
    public async Task AuctionOrderSettlement_IsNotExposedThroughGateway()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var proxyConfig = factory.Services.GetRequiredService<IProxyConfigProvider>().GetConfig();

        proxyConfig.Routes
            .Where(r => r.ClusterId == "commerce-cluster")
            .Select(r => r.Match.Path)
            .Should().NotContain(path => path != null && path.StartsWith("/api/v1/auction-orders/{**", System.StringComparison.Ordinal),
                "a catch-all on /api/v1/auction-orders would forward POST /api/v1/auction-orders/settlement");

        var client = factory.CreateClient();
        var response = await client.PostAsync("/api/v1/auction-orders/settlement", new System.Net.Http.StringContent("{}"));
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
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
