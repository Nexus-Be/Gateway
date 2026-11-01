namespace Gateway.Models;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public sealed record ApiError(string Message, string? Field = null, string? Code = null);

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; } = true;

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; } = 200;

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError[]? Errors { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}

public sealed class ApiErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "One or more validation errors occurred.";

    [JsonPropertyName("status")]
    public int Status { get; init; } = 400;

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; init; } = "";

    [JsonPropertyName("detail")]
    public string Detail { get; init; } = "";

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
