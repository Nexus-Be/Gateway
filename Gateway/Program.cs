using System;
using System.Text;
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

// 3. JWT Authentication
var jwtConfig = builder.Configuration.GetSection("Jwt");
var signature = jwtConfig["Signature"] ?? Environment.GetEnvironmentVariable("JWT__SIGNATURE");

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
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwtConfig["Issuer"]),
            ValidIssuer = jwtConfig["Issuer"],
            ValidateAudience = !string.IsNullOrWhiteSpace(jwtConfig["Audience"]),
            ValidAudience = jwtConfig["Audience"],
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

// 4. Redis Connection
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] 
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

// 5. YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(b => b.AddIdentityPropagation());

var app = builder.Build();

// 6. Middleware Pipeline Ordering
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<ClientContextMiddleware>();
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SensitiveRateLimitMiddleware>();
app.UseMiddleware<AdminAuthorizationGuardMiddleware>();
app.UseMiddleware<IdempotencyGuardMiddleware>();
app.UseMiddleware<ResponseEnvelopeMiddleware>();

// 7. Health & Status endpoints
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Nexus.Gateway",
    timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
}));

app.MapGet("/", () => Results.Ok(new
{
    service = "Nexus.Gateway",
    status = "Running",
    timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
}));

// 8. YARP Reverse Proxy Endpoint
app.MapReverseProxy();

app.Run();

public partial class Program { }
