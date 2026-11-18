using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gateway.Middlewares.Global;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gateway.Tests;

public class ResponseEnvelopeAndCorsTests
{
    [Theory]
    [InlineData("/api/v1/wallets/vnpay/ipn")]
    [InlineData("/api/v1/wallets/stripe/webhooks")]
    [InlineData("/api/v1/webhooks/ghn")]
    [InlineData("/webhooks/ghn")]
    [InlineData("/api/v1/fulfillment/webhook/ghn")]
    [InlineData("/api/v1/fulfillment/webhooks/ghn")]
    [InlineData("/hubs/auctions/negotiate")]
    public async Task ResponseEnvelopeMiddleware_SkipsWebhooksAndSignalRHubs(string path)
    {
        const string rawPayload = "{\"connectionId\":\"abc\",\"availableTransports\":[]}";
        var middleware = new ResponseEnvelopeMiddleware(async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(rawPayload);
        });

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await middleware.InvokeAsync(context);

        responseBody.Position = 0;
        var body = Encoding.UTF8.GetString(responseBody.ToArray());
        body.Should().Be(rawPayload);
    }

    [Fact]
    public async Task Cors_AllowsCredentialsAndEchoesOrigin_ForSignalRNegotiate()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/auctions/negotiate");
        request.Headers.Add("Origin", "http://192.168.1.50:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).Should().BeTrue();
        origins!.First().Should().Be("http://192.168.1.50:3000");
        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var creds).Should().BeTrue();
        creds!.First().Should().Be("true");
    }
}
