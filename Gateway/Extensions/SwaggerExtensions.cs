namespace Gateway.Extensions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

public static class SwaggerExtensions
{
    private const string SwaggerPathPrefix = "/swagger";
    private const string DownstreamDocumentPath = "/swagger/downstream/{service}/swagger.json";
    private const string DownstreamSwaggerSuffix = "/swagger/v1/swagger.json";
    private const string HttpClientName = "swagger-downstream";

    public static readonly IReadOnlyDictionary<string, string> ServiceClusters =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user"] = "user-cluster",
            ["catalog"] = "catalog-cluster",
            ["commerce"] = "commerce-cluster",
            ["log"] = "log-cluster",
            ["auction"] = "auction-cluster",
            ["fulfillment"] = "fulfillment-cluster",
            ["notification"] = "notification-cluster",
            ["wallet"] = "wallet-cluster",
            ["checkheal"] = "checkheal-cluster"
        };

    public static readonly IReadOnlyDictionary<string, string> DownstreamDisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["user"] = "User Service",
            ["catalog"] = "Catalog Service",
            ["commerce"] = "Commerce Service",
            ["log"] = "Log Service",
            ["auction"] = "Auction Service",
            ["fulfillment"] = "Fulfillment Service",
            ["notification"] = "Notification Service",
            ["wallet"] = "Wallet Service",
            ["checkheal"] = "CheckHeal Service"
        };

    private static readonly string[] StrippedSecuritySchemes =
    {
        "x-user-id",
        "x-user-roles",
        "x-user-email",
        "x-user-permissions",
        "X-Internal-Service-Key"
    };

    public static IServiceCollection AddGatewaySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Nexus API Gateway",
                Version = "v1",
                Description = "Stateless edge reverse proxy for the Nexus ecosystem. Downstream service contracts are exposed through /swagger/downstream/{service}/swagger.json."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter a valid JWT Bearer token."
            });

            var xmlPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{typeof(SwaggerExtensions).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        services.AddHttpClient(HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }

    public static WebApplication UseGatewaySwagger(this WebApplication app)
    {
        if (!IsSwaggerEnabled(app))
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.DocExpansion(DocExpansion.List);
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();

            foreach (var service in ServiceClusters.Keys)
            {
                options.SwaggerEndpoint(
                    $"/swagger/downstream/{service}/swagger.json",
                    DownstreamDisplayNames[service]);
            }

            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway (Edge)");
        });

        return app;
    }

    public static WebApplication MapGatewaySwaggerProxies(this WebApplication app)
    {
        if (!IsSwaggerEnabled(app))
        {
            return app;
        }

        app.MapGet(DownstreamDocumentPath, HandleDownstreamDocumentAsync)
            .ExcludeFromDescription();

        return app;
    }

    public static bool IsSwaggerEnabled(WebApplication app)
        => app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("Swagger:Enabled");

    private static async Task<IResult> HandleDownstreamDocumentAsync(
        string service,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Gateway.SwaggerDownstream");

        if (!ServiceClusters.TryGetValue(service, out var clusterId))
        {
            return UpstreamUnavailable($"Unknown downstream service '{service}'.");
        }

        var address = configuration
            .GetSection($"ReverseProxy:Clusters:{clusterId}:Destinations")
            .GetChildren()
            .Select(destination => destination["Address"])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(address))
        {
            logger.LogWarning("No destination address configured for cluster '{ClusterId}'", clusterId);
            return UpstreamUnavailable($"Downstream address for cluster '{clusterId}' is not configured.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var documentUrl = $"{address.TrimEnd('/')}{DownstreamSwaggerSuffix}";

        try
        {
            using var response = await client.GetAsync(documentUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Downstream Swagger document request to '{DocumentUrl}' returned status {StatusCode}",
                    documentUrl,
                    (int)response.StatusCode);
                return UpstreamUnavailable($"Downstream Swagger document for '{service}' is unavailable.");
            }

            var document = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.Text(SanitizeDocument(document), "application/json");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Downstream Swagger document request to '{DocumentUrl}' failed", documentUrl);
            return UpstreamUnavailable($"Downstream Swagger document for '{service}' is unreachable.");
        }
    }

    private static IResult UpstreamUnavailable(string message)
        => Results.Json(
            new { errorCode = "SWAGGER_UPSTREAM_UNAVAILABLE", message },
            statusCode: StatusCodes.Status502BadGateway);

    public static string SanitizeDocument(string document)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(document);
        }
        catch (JsonException)
        {
            return document;
        }

        if (root is not JsonObject openApiDocument)
        {
            return document;
        }

        if (openApiDocument["paths"] is JsonObject paths)
        {
            var internalPaths = paths
                .Select(entry => entry.Key)
                .Where(path => path.StartsWith("/api/v1/internal", StringComparison.OrdinalIgnoreCase) ||
                               path.Contains("/internal/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var path in internalPaths)
            {
                paths.Remove(path);
            }
        }

        if (openApiDocument["components"] is JsonObject components &&
            components["securitySchemes"] is JsonObject securitySchemes)
        {
            foreach (var scheme in StrippedSecuritySchemes)
            {
                securitySchemes.Remove(scheme);
            }
        }

        if (openApiDocument["security"] is JsonArray security)
        {
            for (var index = security.Count - 1; index >= 0; index--)
            {
                if (security[index] is not JsonObject requirement)
                {
                    continue;
                }

                foreach (var scheme in StrippedSecuritySchemes)
                {
                    requirement.Remove(scheme);
                }

                if (requirement.Count == 0)
                {
                    security.RemoveAt(index);
                }
            }

            if (security.Count == 0)
            {
                openApiDocument.Remove("security");
            }
        }

        return openApiDocument.ToJsonString();
    }
}
