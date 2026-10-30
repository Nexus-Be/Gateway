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
    [JsonPropertyName("success")]
    public bool Success { get; init; } = false;

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; } = 400;

    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
}
