namespace Gateway.Middlewares.Global;

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Gateway.Models;
using Microsoft.AspNetCore.Http;

public sealed class ResponseEnvelopeMiddleware
{
    private readonly RequestDelegate _next;

    public ResponseEnvelopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 0. Skip envelope for Swagger docs and third-party webhooks requiring flat protocol payloads
        if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/api/v1/wallets/vnpay/ipn", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;
        await using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Position = 0;

            // 1. Skip non-success status codes, empty responses, redirects, and 204 NoContent
            if (context.Response.StatusCode == StatusCodes.Status204NoContent ||
                context.Response.StatusCode < 200 ||
                context.Response.StatusCode >= 300 ||
                memoryStream.Length == 0)
            {
                await memoryStream.CopyToAsync(originalBodyStream);
                return;
            }

            // 2. Skip non-JSON content types (e.g. files, images, html, octet-stream)
            var contentType = context.Response.ContentType;
            if (string.IsNullOrWhiteSpace(contentType) ||
                !contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                await memoryStream.CopyToAsync(originalBodyStream);
                return;
            }

            // 3. Inspect JSON payload
            using var reader = new StreamReader(memoryStream, leaveOpen: true);
            var responseBody = await reader.ReadToEndAsync();

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(responseBody);
            }
            catch (JsonException)
            {
                // Not valid JSON, copy original as-is
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);
                return;
            }

            using (doc)
            {
                var root = doc.RootElement;

                // If already enveloped (has boolean "success" AND envelope metadata), pass through directly
                var isAlreadyEnveloped = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("success", out var successElement) &&
                    (successElement.ValueKind == JsonValueKind.True || successElement.ValueKind == JsonValueKind.False) &&
                    (root.TryGetProperty("statusCode", out _) ||
                     root.TryGetProperty("traceId", out _) ||
                     root.TryGetProperty("timestamp", out _) ||
                     root.TryGetProperty("errorCode", out _));

                if (isAlreadyEnveloped)
                {
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(originalBodyStream);
                    return;
                }

                // 4. Wrap raw data into ApiResponse<T>
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
                var envelopeMessage = "Operation completed successfully.";

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("message", out var msgElement) &&
                    msgElement.ValueKind == JsonValueKind.String)
                {
                    var customMsg = msgElement.GetString();
                    if (!string.IsNullOrWhiteSpace(customMsg))
                    {
                        envelopeMessage = customMsg.Trim();
                    }
                }

                var wrapped = new ApiResponse<JsonElement>
                {
                    Success = true,
                    StatusCode = context.Response.StatusCode,
                    Message = envelopeMessage,
                    Data = root.Clone(),
                    TraceId = traceId,
                    Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var wrappedBytes = JsonSerializer.SerializeToUtf8Bytes(wrapped);
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength = wrappedBytes.Length;

                await originalBodyStream.WriteAsync(wrappedBytes);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
