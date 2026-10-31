using System;
using System.Text;
using Gateway.Extensions;
using Gateway.Middlewares.Features;
using Gateway.Middlewares.Global;
using Gateway.Transforms;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog Logging
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

// 2. CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Trace-Id", "X-Content-Type-Options", "Retry-After");
    });
});

// 3. Swagger / OpenAPI aggregation
builder.Services.AddGatewaySwagger();

// 4. JWT Authentication
var jwtConfig = builder.Configuration.GetSection("Jwt");
var signature = Environment.GetEnvironmentVariable("JWT_SIGNATURE")
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? jwtConfig["Signature"]
    ?? Environment.GetEnvironmentVariable("JWT__SIGNATURE");

var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
    ?? Environment.GetEnvironmentVariable("JWT__ISSUER")
    ?? jwtConfig["Issuer"]
    ?? "http://localhost:8001";

var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
    ?? Environment.GetEnvironmentVariable("JWT__AUDIENCE")
    ?? jwtConfig["Audience"]
    ?? "shoppingcart-api";

if (!string.IsNullOrWhiteSpace(signature))
{
    var key = Encoding.UTF8.GetBytes(signature);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = jwtConfig.GetValue<bool>("RequireHttpsMetadata");
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidIssuer = issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
}
else
{
    builder.Services.AddAuthentication();
}

builder.Services.AddAuthorization();

// 5. Redis Connection
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? builder.Configuration["Redis:ConnectionString"] 
    ?? builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";

try
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false;
    config.ConnectTimeout = 2000;
    var redis = ConnectionMultiplexer.Connect(config);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
}
catch (Exception)
{
    // In local dev/test where Redis is not available, Redis will be skipped and in-memory fallback will activate
}

// 6. YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(b => b.AddIdentityPropagation());

var app = builder.Build();

// 7. Middleware Pipeline Ordering
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<ClientContextMiddleware>();
app.UseCors("AllowFrontend");

app.UseGatewaySwagger();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SensitiveRateLimitMiddleware>();
app.UseMiddleware<AdminAuthorizationGuardMiddleware>();
app.UseMiddleware<IdempotencyGuardMiddleware>();
app.UseMiddleware<ResponseEnvelopeMiddleware>();

// 8. Health & Status endpoints
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Nexus.Gateway",
    timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
}));

app.MapGet("/", () =>
    SwaggerExtensions.IsSwaggerEnabled(app)
        ? Results.Redirect("/swagger")
        : Results.Ok(new
        {
            service = "Nexus.Gateway",
            status = "Running",
            timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        }));

// 9. Swagger aggregation proxies
app.MapGatewaySwaggerProxies();

// 10. YARP Reverse Proxy Endpoint
app.MapReverseProxy();

app.Run();

public partial class Program { }
