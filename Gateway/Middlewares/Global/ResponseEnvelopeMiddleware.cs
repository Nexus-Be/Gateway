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

                // If already enveloped (has "success" property), pass through directly
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("success", out _))
                {
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(originalBodyStream);
                    return;
                }

                // 4. Wrap raw data into ApiResponse<T>
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
                var wrapped = new ApiResponse<JsonElement>
                {
                    Success = true,
                    StatusCode = context.Response.StatusCode,
                    Message = "Operation completed successfully.",
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
