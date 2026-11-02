namespace Gateway.Extensions;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddGatewayObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] 
            ?? configuration["Otel:Endpoint"] 
            ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("Nexus.Gateway"))
            .WithTracing(t =>
            {
                t.AddSource("Yarp.ReverseProxy")
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.Filter = httpContext =>
                    {
                        var path = httpContext.Request.Path.Value;
                        // Exclude trivial health probes and swagger assets from root traces
                        return path != null && !path.StartsWith("/health") && !path.StartsWith("/swagger");
                    };
                })
                .AddHttpClientInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
