namespace Gateway.Authentication;

using System;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Service-to-service authentication for the notifications that AuctionService sends. It is a second, separate JWT scheme: it has its own
/// issuer, audience and signing key, so a user token never passes it and a service token never passes the user scheme.
/// A route opts in with <c>AuthorizationPolicy: "AuctionNotificationWrite"</c> in the YARP configuration.
/// </summary>
public static class AuctionNotificationServiceAuth
{
    public const string Scheme = "AuctionNotificationService";
    public const string Policy = "AuctionNotificationWrite";
    public const string Issuer = "auction-service";
    public const string Audience = "nexus-auction-notifications";
    public const string RequiredScope = "auction.notifications.write";
    public const string DefaultServiceUserId = "00000000-0000-0000-0000-0000000000a1";

    /// <summary>Signing key (at least 32 UTF-8 bytes) from <c>AUCTION_NOTIFICATION_SIGNING_KEY</c> or <c>AuctionNotificationAuth:SigningKey</c>.</summary>
    public static string? ReadSigningKey(IConfiguration configuration) =>
        Environment.GetEnvironmentVariable("AUCTION_NOTIFICATION_SIGNING_KEY") ?? configuration["AuctionNotificationAuth:SigningKey"];

    /// <summary>The only subject (<c>sub</c>) that may call these routes: Auction's configured notification service user.</summary>
    public static string ReadServiceUserId(IConfiguration configuration) =>
        Environment.GetEnvironmentVariable("AUCTION_NOTIFICATION_SERVICE_USER_ID")
        ?? configuration["AuctionNotificationAuth:ServiceUserId"]
        ?? DefaultServiceUserId;

    /// <summary>
    /// Registers the scheme. The settings are read when the options are first built (not while the host is still being assembled), so they
    /// come from the final configuration, including values supplied by tests or by the environment after start-up.
    /// </summary>
    public static AuthenticationBuilder AddAuctionNotificationServiceScheme(this AuthenticationBuilder builder)
    {
        builder.AddJwtBearer(Scheme, _ => { });
        builder.Services.AddOptions<JwtBearerOptions>(Scheme).Configure<IConfiguration>((options, configuration) =>
        {
            var signingKey = ReadSigningKey(configuration);
            var usable = !string.IsNullOrWhiteSpace(signingKey) && Encoding.UTF8.GetByteCount(signingKey) >= 32;

            // Keep the claim names as sent ("sub", "scope"): the policy below matches on them.
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                // Without a usable key every token is rejected (fail closed); the scheme never falls back to the user key.
                IssuerSigningKey = usable ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!)) : null,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(15)
            };
        });
        return builder;
    }

    public static IServiceCollection AddAuctionNotificationWritePolicy(this IServiceCollection services)
    {
        services.AddOptions<AuthorizationOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var serviceUserId = ReadServiceUserId(configuration);
            options.AddPolicy(Policy, policy =>
            {
                policy.AddAuthenticationSchemes(Scheme);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("sub", serviceUserId);
                policy.RequireClaim("scope", RequiredScope);
            });
        });
        return services;
    }
}
